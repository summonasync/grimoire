namespace Grimoire.Mcp.Projections;

internal sealed record ProjectedListView
{
    public required string Tome { get; init; }

    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Memories { get; init; }

    public required int Count { get; init; }

    public IReadOnlyList<string>? Warnings { get; init; }
}
