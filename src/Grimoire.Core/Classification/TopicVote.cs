namespace Grimoire.Core;

public sealed record TopicVote
{
    public required string? Tag { get; init; }

    public string? CandidateTag { get; init; }

    public required TopicVoteDecision Decision { get; init; }

    public int WinningCount { get; init; }

    public double? BestSimilarity { get; init; }

    public IReadOnlyList<TopicVoteNeighbor> Neighbors { get; init; } = [];
}
