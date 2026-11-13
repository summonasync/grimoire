namespace Grimoire.Core;

public sealed class EmbeddingTopicClassifier(
    IMemoryStore store,
    MemoryEngineOptions? options = null) : ITopicClassifier
{
    private const int NeighborLimit = 5;

    private readonly MemoryEngineOptions _options = options ?? new();

    public async Task<TopicVote> ClassifyAsync(
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

        var neighbors = matches
            .Where(match => match.Similarity >= _options.NearTopicSimilarityThreshold)
            .ToArray();

        IReadOnlyList<TopicVoteNeighbor> evidence = [
            .. neighbors
                .SelectMany(match => EvidenceOf(match.Record, match.Similarity))
                .OrderByDescending(entry => entry.Similarity)
                .ThenBy(entry => entry.Tag, StringComparer.Ordinal),
        ];

        var winner = neighbors
            .SelectMany(match => TopicTagsOf(match.Record))
            .GroupBy(tag => tag, StringComparer.Ordinal)
            .Where(group => group.Count() >= _options.TopicAgreementCount)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .FirstOrDefault();

        if (winner is null)
        {
            return new TopicVote
            {
                Tag = null,
                Decision = TopicVoteDecision.NoConsensus,
                Neighbors = evidence,
            };
        }

        var bestSimilarity = neighbors
            .Where(match => TopicTagsOf(match.Record).Contains(winner.Key, StringComparer.Ordinal))
            .Max(match => match.Similarity);

        if (_options.TopicConfidenceThreshold > 0 && bestSimilarity < _options.TopicConfidenceThreshold)
        {
            return new TopicVote
            {
                Tag = null,
                CandidateTag = winner.Key,
                Decision = TopicVoteDecision.BelowConfidence,
                WinningCount = winner.Count(),
                BestSimilarity = bestSimilarity,
                Neighbors = evidence,
            };
        }

        return new TopicVote
        {
            Tag = winner.Key,
            CandidateTag = winner.Key,
            Decision = TopicVoteDecision.Voted,
            WinningCount = winner.Count(),
            BestSimilarity = bestSimilarity,
            Neighbors = evidence,
        };
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

    private static bool IsTopicTag(string tag) =>
        tag.StartsWith(ITopicClassifier.TopicPrefix, StringComparison.Ordinal)
        && tag.Length > ITopicClassifier.TopicPrefix.Length;

    private static IEnumerable<string> TopicTagsOf(MemoryRecord record) =>
        record.Tags.Where(IsTopicTag);

    private static IEnumerable<TopicVoteNeighbor> EvidenceOf(MemoryRecord record, double similarity) => record.Tags
        .Where(tag => !string.IsNullOrWhiteSpace(tag) && tag != PersonaSectionExtensions.PersonaTag)
        .Select(tag => new TopicVoteNeighbor(tag, similarity, IsTopicTag(tag)));
}
