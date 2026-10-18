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
        var scope = CliValues.Scope(invocation.Spec, invocation.Value("scope"), MemoryScope.Project);
        var ttlHours = invocation.Value("ttl") is { } ttlText ? ParseTtlHours(invocation.Spec, scope, ttlText) : (double?)null;
        var request = new RememberRequest
        {
            Content = invocation.JoinedPositional(),
            Scope = scope,
            Kind = CliValues.Kind(invocation.Spec, invocation.Value("kind"), MemoryKind.Journal),
            Actor = ActorResolver.Resolve(environment, invocation.Value("actor")),
            ProjectId = invocation.Value("project"),
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

        environment.Output.WriteLine($"Remembered {result.Record.Id} ({request.Scope}/{request.Kind} by {request.Actor})");
        return ExitCodes.Ok;
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
