using System.ComponentModel;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class StatsTool(MemoryService memories, IMemoryStore store, MemoryActorProvider actors)
{
    public const string Name = "memory_stats";

    public const string Title = "Get memory stats";

    public const string Description =
        "Report store health: active, superseded and tombstoned counts, records per scope, idempotencyConflicts, " +
        "the gc heartbeat and the consolidation heartbeat (lastAttemptAt/lastSuccessAt/consolidatedClusters, stale=true " +
        "means the sweep is overdue and stale journal clusters keep piling up); " +
        "pendingCompaction is the whole-store compaction backlog across all tomes while memory_brief.compactionDue " +
        "scopes the briefed tome; byTome splits active and tombstoned per tome while counts span the whole store. " +
        "Opt into pendingCompactionByTome when you need the backlog split per tome - it plans every tome's journal " +
        "and costs extra, so it is skipped by default. Call it after a failed memory call to check reachability, " +
        "or to size the compaction loop before working candidates.";

    public Task<CallToolResult> GetStats(
        [Description("Tome named in the response as yours; omit unless you want to declare another tome you own. Stats themselves always span the whole store.")]
        string? tome = null,
        [Description("Set true to also get pendingCompactionByTome: the stale-journal backlog planned per tome. Off by default - planning every tome's journal is the expensive part of stats.")]
        bool includeTomeCompaction = false,
        CancellationToken cancellationToken = default)
    {
        if (TomeParams.Validate(tome, nameof(tome)) is { } tomeError)
        {
            return Task.FromResult(ToolResults.Failure(tomeError));
        }

        return ToolResults.ExecuteAsync(async ct =>
        {
            var stats = await memories.StatsAsync(ct);
            var resolvedTome = await actors.ResolveTomeAsync(tome, ct);

            return ToolResults.Success(new StatsView
            {
                Tome = resolvedTome,
                Active = stats.Active,
                Superseded = stats.Superseded,
                Tombstoned = stats.Tombstoned,
                ActiveByScope = stats.ActiveByScope,
                PendingCompaction = stats.PendingCompaction,
                IdempotencyConflicts = stats.IdempotencyConflicts,
                ByTome = stats.ByTome,
                PendingCompactionByTome = includeTomeCompaction
                    ? await memories.PendingCompactionByTomeAsync(ct)
                    : null,
                Gc = stats.Gc,
                Consolidation = stats.Consolidation,
                Warnings = await EmptyTomeWarning.BuildAsync(store, stats.ByTome.Count == 0, tome, resolvedTome, ct),
            });
        }, cancellationToken);
    }
}
