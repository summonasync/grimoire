using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class CompactCandidatesTool(MemoryService memories, IMemoryStore store)
{
    public const string Name = "memory_compact_candidates";

    public const string Title = "Get compaction candidates";

    public const string Description =
        "Fetch clusters of old, similar journal entries that are due for distillation into durable facts; their " +
        "total record count is also reported as pendingCompaction in memory_stats. Run the loop when " +
        "pendingCompaction grows or the human asks to compact: for each group, write one kind=fact " +
        "memory_remember capturing the durable point, then memory_forget each consumed entry with a reason like " +
        "\"compacted into <new memory id>\". This is how journal noise becomes the standing encyclopedia - distill " +
        "deliberately, never speculatively, and never tombstone entries you did not actually distill.";

    public Task<CallToolResult> GetCandidates(CancellationToken cancellationToken = default)
    {
        return ToolResults.ExecuteAsync(async ct =>
        {
            var groups = await memories.GetCompactionCandidatesAsync(ct);
            var views = new List<CompactGroupView>(groups.Count);

            foreach (var group in groups)
            {
                var records = await Task.WhenAll(group.MemoryIds.Select(id => store.GetAsync(id, ct)));
                views.Add(new CompactGroupView
                {
                    MemoryIds = group.MemoryIds,
                    Similarity = group.Similarity,
                    StalestAccess = group.StalestAccess,
                    Entries = [.. records
                        .Where(record => record is not null)
                        .Select(record => new CompactEntryView
                        {
                            MemoryId = record!.Id,
                            Content = record.Content,
                            CreatedAt = record.CreatedAt,
                            LastAccessedAt = record.LastAccessedAt,
                        })],
                });
            }

            return ToolResults.Success(new CompactCandidatesView
            {
                Groups = views,
                CandidateCount = views.Sum(group => group.Entries.Count),
            });
        }, cancellationToken);
    }
}
