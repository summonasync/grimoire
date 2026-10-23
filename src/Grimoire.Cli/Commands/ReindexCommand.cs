using System.Diagnostics;
using Grimoire.Cli.CommandLine;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class ReindexCommand(CliEnvironment environment)
{
    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var fingerprint = EmbeddingFingerprint.From(config.Embedding.Model, config.Embedding.Dimensions);
        var reindexer = services.GetRequiredService<EmbeddingReindexer>();
        var clock = Stopwatch.StartNew();
        ReindexProgress last = default;

        var outcome = await reindexer.RunAsync(
            fingerprint,
            progress =>
            {
                last = progress;
                environment.Output.WriteLine($"{progress.Processed}/{progress.Total} reindexed (eta {Eta(clock.Elapsed, progress)}s)");
            },
            cancellationToken);

        if (outcome.Recreated)
        {
            environment.Output.WriteLine(
                $"Collection '{config.Qdrant.Collection}' was recreated under {fingerprint.Dimensions} dimensions " +
                $"for '{fingerprint.Model}'; {outcome.Discarded} active records of the previous embedding were discarded");
        }

        environment.Output.WriteLine(
            $"Reindex complete: {outcome.Reembedded} re-embedded, {outcome.Skipped} already current, " +
            $"{outcome.Processed} scanned with '{config.Embedding.Model}' in {clock.Elapsed.TotalSeconds:F0}s");
        return ExitCodes.Ok;
    }

    private static long Eta(TimeSpan elapsed, ReindexProgress progress) =>
        progress.Processed == 0 || progress.Processed >= progress.Total
            ? 0
            : (long)(elapsed.TotalSeconds / progress.Processed * (progress.Total - progress.Processed));
}
