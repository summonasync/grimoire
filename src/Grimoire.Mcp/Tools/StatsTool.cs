using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class StatsTool(MemoryService memories)
{
    public const string Name = "memory_stats";

    public const string Title = "Get memory stats";

    public const string Description =
        "Report store health: active, superseded and tombstoned counts, active records per scope, and " +
        "pendingCompaction (journal entries clustered and waiting to be distilled into facts). Call it to check " +
        "the store is reachable after a failed memory call, to size the compaction backlog before running " +
        "memory_compact_candidates, or when the human asks what memory currently holds.";

    public Task<CallToolResult> GetStats(CancellationToken cancellationToken = default)
    {
        return ToolResults.ExecuteAsync(async ct =>
        {
            var stats = await memories.StatsAsync(ct);

            return ToolResults.Success(new StatsView
            {
                Active = stats.Active,
                Superseded = stats.Superseded,
                Tombstoned = stats.Tombstoned,
                ActiveByScope = stats.ActiveByScope,
                PendingCompaction = stats.PendingCompaction,
            });
        }, cancellationToken);
    }
}
