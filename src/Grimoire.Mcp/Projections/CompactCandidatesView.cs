namespace Grimoire.Mcp.Projections;

internal sealed record CompactCandidatesView
{
    public required IReadOnlyList<CompactGroupView> Groups { get; init; }

    public required int CandidateCount { get; init; }
}
