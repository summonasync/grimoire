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
        "Report store health: active, superseded and tombstoned counts, active records per scope, " +
        "pendingCompaction (journal entries clustered and waiting to be distilled into facts), and " +
        "idempotencyConflicts (active duplicates whose supersede maintenance never finished; 0 is healthy, " +
        "replaying the same write converges them). Counts span the whole store; byTome splits active and " +
        "tombstoned per tome and tome names the one your tools resolve to. Call it to check the store is " +
        "reachable after a failed memory call, to size the compaction backlog before running " +
        "memory_compact_candidates, or when the human asks what memory currently holds.";

    public Task<CallToolResult> GetStats(
        [Description("Tome named in the response as yours; omit unless you want to declare another tome you own. Stats themselves always span the whole store.")]
        string? tome = null,
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
            });
        }, cancellationToken);
    }
}
