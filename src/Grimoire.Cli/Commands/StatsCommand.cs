using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class StatsCommand(CliEnvironment environment)
{
    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var identity = await ActorResolver.ResolveAsync(
            environment, null, null, config.DefaultTome,
            services.GetRequiredService<IActorClaimProvider>(), cancellationToken);
        var memories = services.GetRequiredService<MemoryService>();
        var stats = await memories.StatsAsync(cancellationToken);
        environment.Output.WriteLine($"Active: {stats.Active} ({ScopeBreakdown(stats)})");
        environment.Output.WriteLine($"Superseded: {stats.Superseded}");
        environment.Output.WriteLine($"Tombstoned: {stats.Tombstoned}");
        environment.Output.WriteLine($"Pending compaction: {stats.PendingCompaction}");
        environment.Output.WriteLine($"Idempotency conflicts: {stats.IdempotencyConflicts}");
        environment.Output.WriteLine($"GC: {GcText(stats.Gc)}");
        environment.Output.WriteLine($"Consolidation: {ConsolidationText(stats.Consolidation)}");
        environment.Output.WriteLine($"Tome: {identity.Tome} ({SourceText(identity.Source)})");
        environment.Output.WriteLine($"Per tome: {PerTome(stats)}");
        environment.Output.WriteLine($"Per project: {await PerProjectAsync(memories, identity.Tome, cancellationToken)}");

        if (invocation.Value("include-tome-compaction") is not null)
        {
            environment.Output.WriteLine(
                $"Per-tome compaction: {PerTomeCompaction(await memories.PendingCompactionByTomeAsync(cancellationToken))}");
        }

        return ExitCodes.Ok;
    }

    private const string CrossProjectKey = "cross-project";

    private static async Task<string> PerProjectAsync(
        MemoryService memories,
        string tome,
        CancellationToken cancellationToken)
    {
        var records = await memories.ListAsync(
            new MemoryFilter { Tome = tome, Status = MemoryStatus.Active },
            cancellationToken);
        var counts = new Dictionary<string, int>();

        foreach (var record in records)
        {
            var key = record.ProjectId ?? CrossProjectKey;
            counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
        }

        return counts.Count == 0
            ? "(none)"
            : string.Join(
                ", ",
                counts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key} ({pair.Value})"));
    }

    private static string PerTomeCompaction(IReadOnlyDictionary<string, long> pending) => pending.Count == 0
        ? "(none)"
        : string.Join(
            ", ",
            pending.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key} ({pair.Value})"));

    private static string ScopeBreakdown(MemoryStats stats) => string.Join(
        ", ",
        MemoryScopes.All.Select(scope => $"{scope.ToString().ToLowerInvariant()}: {stats.ActiveByScope.GetValueOrDefault(scope)}"));

    private static string GcText(GcState gc) => gc.IsUnknown
        ? $"unknown ({gc.UnknownReason}), stale: {FormatFlag(gc.Stale)}"
        : $"last attempt {Timestamp(gc.LastAttemptAt)}, last success {Timestamp(gc.LastSuccessAt)}, " +
          $"removed {FormatCount(gc.RemovedExpired)} expired / {FormatCount(gc.RemovedTombstones)} tombstones, " +
          $"last error: {gc.LastError ?? "none"}, stale: {FormatFlag(gc.Stale)}";

    private static string ConsolidationText(ConsolidationState consolidation) => consolidation.IsUnknown
        ? $"unknown ({consolidation.UnknownReason}), stale: {FormatFlag(consolidation.Stale)}"
        : $"last attempt {Timestamp(consolidation.LastAttemptAt)}, last success {Timestamp(consolidation.LastSuccessAt)}, " +
          $"clusters {FormatCount(consolidation.ConsolidatedClusters)}, " +
          $"last error: {consolidation.LastError ?? "none"}, stale: {FormatFlag(consolidation.Stale)}";

    private static string Timestamp(DateTimeOffset? moment) => moment?.ToString("u") ?? "never";

    private static string FormatCount(int? count) => count?.ToString() ?? "?";

    private static string FormatFlag(bool flag) => flag ? "true" : "false";

    private static string PerTome(MemoryStats stats) => stats.ByTome.Count == 0
        ? "(none)"
        : string.Join(
            ", ",
            stats.ByTome
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key} ({pair.Value.Active} active, {pair.Value.Tombstoned} tombstoned, lastWrite {LastWrite(stats, pair.Key)})"));

    private static string LastWrite(MemoryStats stats, string tome) => stats.LastWriteByTome.TryGetValue(tome, out var moment)
        ? Timestamp(moment)
        : "unknown";

    private static string SourceText(TomeSource source) => source switch
    {
        TomeSource.ExplicitParam => "explicit parameter",
        TomeSource.EnvTome => "env tome",
        TomeSource.EnvActor => "env actor",
        TomeSource.ClaimFile => "claim file",
        TomeSource.ClientInfo => "client info",
        _ => "default",
    };
}
