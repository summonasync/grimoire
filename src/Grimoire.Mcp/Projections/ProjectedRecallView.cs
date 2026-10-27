namespace Grimoire.Mcp.Projections;

internal sealed record ProjectedRecallView
{
    public required string Tome { get; init; }

    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Matches { get; init; }

    public required int Count { get; init; }
}
