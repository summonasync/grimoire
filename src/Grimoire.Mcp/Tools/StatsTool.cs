using System.ComponentModel;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class StatsTool(MemoryService memories, MemoryActorProvider actors)
{
    public const string Name = "memory_stats";

    public const string Title = "Get memory stats";

    public const string Description =
        "Report store health: active, superseded and tombstoned counts, records per scope, the pendingCompaction " +
        "backlog, idempotencyConflicts and the gc heartbeat, where stale=true means the sweep is overdue and " +
        "expired records may be piling up; byTome splits active and tombstoned per tome while counts span the " +
        "whole store. Call it after a failed memory call to check reachability, or to size the compaction loop " +
        "before working candidates. Opt into pendingCompactionByTome when you need the backlog split per tome - " +
        "it plans every tome's journal and costs extra, so it is skipped by default.";

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

            return ToolResults.Success(new StatsView
            {
                Tome = await actors.ResolveTomeAsync(tome, ct),
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
            });
        }, cancellationToken);
    }
}
