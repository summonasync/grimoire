namespace Grimoire.Core;

public sealed class EmbeddingTopicClassifier(
    IMemoryStore store,
    MemoryEngineOptions? options = null) : ITopicClassifier
{
    private const int NeighborLimit = 5;

    private readonly MemoryEngineOptions _options = options ?? new();

    public async Task<string?> ClassifyAsync(
        ReadOnlyMemory<float> vector,
        string tome,
        MemoryScope scope,
        string? projectId,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        var matches = await store.SearchAsync(
            vector,
            ProbeFilter(tome, scope, projectId, sessionId),
            NeighborLimit,
            cancellationToken);

        return matches
            .Where(match => match.Similarity >= _options.NearTopicSimilarityThreshold)
            .SelectMany(match => TopicTagsOf(match.Record))
            .GroupBy(tag => tag, StringComparer.Ordinal)
            .Where(group => group.Count() >= _options.TopicAgreementCount)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault();
    }

    private MemoryFilter ProbeFilter(string tome, MemoryScope scope, string? projectId, string? sessionId) => new()
    {
        Scopes = [scope],
        Kind = MemoryKind.Journal,
        Status = MemoryStatus.Active,
        Tome = tome,
        ProjectId = projectId,
        SessionId = sessionId,
        ExcludeTags = [PersonaSectionExtensions.PersonaTag],
    };

    private static IEnumerable<string> TopicTagsOf(MemoryRecord record) => record.Tags
        .Where(tag => tag.StartsWith(ITopicClassifier.TopicPrefix, StringComparison.Ordinal));
}
