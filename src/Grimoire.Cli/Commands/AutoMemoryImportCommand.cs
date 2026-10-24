using System.Text.Json;
using System.Text.Json.Serialization;
using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class AutoMemoryImportCommand(CliEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOutput = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var action = invocation.Positional.FirstOrDefault();
        if (action is not "import")
        {
            throw new UsageException(
                $"Unknown auto-memory action '{action ?? "(none)"}' (expected: import).", invocation.Spec);
        }

        if (invocation.Value("source") is not { } givenSource)
        {
            throw new UsageException("Option 'source' requires the cache directory to import.", invocation.Spec);
        }

        var source = Path.GetFullPath(givenSource, environment.WorkingDirectory);
        if (!Directory.Exists(source))
        {
            throw new CommandException("source_missing", $"Source directory '{source}' does not exist.", ExitCodes.Usage);
        }

        var identity = await ActorResolver.ResolveAsync(
            environment, null, invocation.Value("tome"), config.DefaultTome,
            services.GetRequiredService<IActorClaimProvider>(), cancellationToken);

        var (projectTag, projectWarning) = ResolveProjectTag(services, source);
        var summary = await services.GetRequiredService<AutoMemoryImporter>()
            .ImportAsync(source, identity.Tome, identity.Actor, projectTag, cancellationToken);

        if (invocation.Value("json") is not null)
        {
            var payload = new
            {
                tome = identity.Tome,
                project = projectTag,
                projectWarning,
                summary,
            };
            environment.Output.WriteLine(JsonSerializer.Serialize(payload, JsonOutput));
        }
        else
        {
            WriteSummary(identity.Tome, projectTag, projectWarning, summary);
        }

        return summary.Failed > 0 ? ExitCodes.Runtime : ExitCodes.Ok;
    }

    private static (string? ProjectId, string? Warning) ResolveProjectTag(IServiceProvider services, string source) =>
        services.GetRequiredService<IProjectRegistry>().ResolveByPath(source) is { } match
            ? (match.Alias, null)
            : (null, $"no registered project matches the source directory '{source}'; importing without a project tag");

    private void WriteSummary(
        string tome,
        string? projectTag,
        string? projectWarning,
        AutoMemoryImportSummary summary)
    {
        environment.Output.WriteLine($"Files: {summary.Files}");
        environment.Output.WriteLine($"Imported: {summary.Imported}");
        environment.Output.WriteLine($"Skipped duplicates: {summary.SkippedDuplicates}");
        environment.Output.WriteLine($"Failed: {summary.Failed}");
        environment.Output.WriteLine($"Unchanged: {summary.Unchanged}");
        environment.Output.WriteLine($"Tome: {tome}");
        environment.Output.WriteLine($"Project: {projectTag ?? "(none)"}");

        foreach (var outcome in summary.Outcomes)
        {
            environment.Output.WriteLine(OutcomeLine(outcome));
        }

        if (projectWarning is { } registryWarning)
        {
            environment.Output.WriteLine($"Warning: {registryWarning}");
        }

        foreach (var importWarning in summary.Warnings)
        {
            environment.Output.WriteLine($"Warning: {importWarning}");
        }
    }

    private static string OutcomeLine(AutoMemoryFileOutcome outcome) => outcome.Kind switch
    {
        AutoMemoryOutcomeKind.Imported => $"{outcome.FileName}: imported ({outcome.RecordId})",
        AutoMemoryOutcomeKind.SkippedDuplicate => $"{outcome.FileName}: skipped, duplicate of {outcome.WinnerId}",
        _ => $"{outcome.FileName}: failed - {outcome.Reason}",
    };
}
