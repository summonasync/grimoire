using System.ComponentModel;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class CompactCandidatesTool(MemoryService memories, IMemoryStore store, MemoryActorProvider actors)
{
    public const string Name = "memory_compact_candidates";

    public const string Title = "Get compaction candidates";

    public const string Description =
        "Fetch clusters of old, similar journal entries due for distillation into durable facts - each group " +
        "carries its entry records, similarity and the stalest access time, and the whole backlog counts as " +
        "memory_stats pendingCompaction. Run the loop deliberately: write one kind=Fact memory_remember per " +
        "group, then memory_forget each consumed entry; distill within one tome at a time.";

    public Task<CallToolResult> GetCandidates(
        [Description("Tome whose journal to distill; omit for your resolved tome. Distilled facts must be written back into the same tome.")]
        string? tome = null,
        CancellationToken cancellationToken = default)
    {
        if (TomeParams.Validate(tome, nameof(tome)) is { } tomeError)
        {
            return Task.FromResult(ToolResults.Failure(tomeError));
        }

        return ToolResults.ExecuteAsync(async ct =>
        {
            var resolvedTome = await actors.ResolveTomeAsync(tome, ct);
            var groups = await memories.GetCompactionCandidatesAsync(ct, resolvedTome);
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
                Tome = resolvedTome,
                Groups = views,
                CandidateCount = views.Sum(group => group.Entries.Count),
            });
        }, cancellationToken);
    }
}
