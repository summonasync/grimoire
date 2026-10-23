using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class RememberCommand(CliEnvironment environment)
{
    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var identity = await ActorResolver.ResolveAsync(
            environment, invocation.Value("actor"), invocation.Value("tome"), config.DefaultTome,
            services.GetRequiredService<IActorClaimProvider>(), cancellationToken);
        var scope = CliValues.Scope(invocation.Spec, invocation.Value("scope"), MemoryScope.Project);
        var ttlHours = invocation.Value("ttl") is { } ttlText ? ParseTtlHours(invocation.Spec, scope, ttlText) : (double?)null;
        var projectId = invocation.Value("project");
        if (scope == MemoryScope.Session && projectId is not null)
        {
            throw new CommandException(
                "param_conflict",
                $"--project only applies to scopes 'project', 'global' and 'personal'; omit it or change the scope " +
                $"(got --project '{projectId}' with scope 'session').",
                ExitCodes.Usage);
        }

        var projectTag = ResolveProjectTag(projectId, scope, services);
        var request = new RememberRequest
        {
            Content = invocation.JoinedPositional(),
            Scope = scope,
            Kind = CliValues.Kind(invocation.Spec, invocation.Value("kind"), MemoryKind.Journal),
            Actor = identity.Actor,
            Tome = identity.Tome,
            ProjectId = projectTag.ProjectId,
            SessionId = invocation.Value("session"),
            Tags = CliValues.Tags(invocation.Value("tags")),
            Importance = invocation.Value("importance") is { } raw
                ? CliValues.Unit(invocation.Spec, "importance", raw)
                : 0.5,
        };

        var memories = services.GetRequiredService<MemoryService>();
        var result = await memories.RememberAsync(request, cancellationToken);

        if (ttlHours is { } hours)
        {
            var clock = services.GetRequiredService<IClock>();
            await services.GetRequiredService<IMemoryStore>()
                .UpdateAsync(result.Record with { ExpiresAt = clock.UtcNow.AddHours(hours) }, cancellationToken);
        }

        if (result.SupersededId is { } superseded)
        {
            environment.Output.WriteLine($"Superseded {superseded}");
        }

        var autoTag = projectTag.AutoTagged ? $", project '{projectTag.ProjectId}' auto-tagged from the working directory" : "";
        environment.Output.WriteLine(
            $"Remembered {result.Record.Id} ({request.Scope}/{request.Kind} by {request.Actor} in tome '{identity.Tome}'{autoTag})");

        if (result.IdempotentReplay)
        {
            environment.Output.WriteLine("Replayed an existing record; nothing new was embedded or superseded.");
        }

        if (result.MaintenancePending)
        {
            environment.Output.WriteLine("Committed, but duplicate maintenance is pending; replay the same command to converge the store.");
        }

        return ExitCodes.Ok;
    }

    private static ProjectTagResolution ResolveProjectTag(string? projectId, MemoryScope scope, IServiceProvider services)
    {
        var callerPath = services.GetRequiredService<ICallerPathProvider>().Path;
        var resolution = services.GetRequiredService<ProjectTagResolver>().Resolve(scope, projectId, callerPath);
        if (resolution.Rejection is not { } rejection)
        {
            return resolution;
        }

        throw rejection switch
        {
            ProjectTagRejection.Unknown => new CommandException("project_id_unknown", UnknownMessage(projectId, resolution), ExitCodes.Usage),
            _ => new CommandException("project_id_required", RequiredMessage(callerPath, resolution), ExitCodes.Usage),
        };
    }

    private static string UnknownMessage(string? projectId, ProjectTagResolution resolution)
    {
        var allowed = string.Join(", ", resolution.AllowedAliases);
        return resolution.AllowedAliases.Count == 0
            ? $"--project '{projectId}' is not a registered project and the project registry is empty - register the project with 'grimoire projects add' first."
            : $"--project: '{projectId}' is not a registered project alias. Allowed --project values " +
              $"(case-sensitive, pass one exactly as advertised): {allowed}.";
    }

    private static string RequiredMessage(string callerPath, ProjectTagResolution resolution)
    {
        var allowed = string.Join(", ", resolution.AllowedAliases);
        return "scope 'project' requires --project (got scope 'project', no --project): the working directory " +
               $"'{callerPath}' matches no registered project path. " +
               (resolution.AllowedAliases.Count == 0
                   ? "The project registry is empty; register the project with 'grimoire projects add' first. "
                   : $"Pass one of the registered --project values (case-sensitive, pass one exactly as advertised): {allowed}. ") +
               "Or use --scope global when the memory is not project-specific.";
    }

    private static double ParseTtlHours(CommandSpec spec, MemoryScope scope, string ttlText)
    {
        if (scope != MemoryScope.Session)
        {
            throw new UsageException("--ttl applies only to session-scope memories.", spec);
        }

        return CliValues.Hours(spec, "ttl", ttlText);
    }
}
