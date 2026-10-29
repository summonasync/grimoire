namespace Grimoire.Mcp.Projections;

internal sealed record RecallView
{
    public required string Tome { get; init; }

    public required IReadOnlyList<RecallHit> Matches { get; init; }

    public required int Count { get; init; }

    public IReadOnlyList<string>? Warnings { get; init; }
}
