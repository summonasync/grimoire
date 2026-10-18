namespace Grimoire.Mcp.Projections;

internal sealed record RecallView
{
    public required IReadOnlyList<RecallHit> Matches { get; init; }

    public required int Count { get; init; }
}
