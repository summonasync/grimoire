namespace Grimoire.Mcp.Projections;

internal sealed record CompactCandidatesView
{
    public required string Tome { get; init; }

    public required IReadOnlyList<CompactGroupView> Groups { get; init; }

    public required int CandidateCount { get; init; }

    public IReadOnlyList<string>? Warnings { get; init; }
}
