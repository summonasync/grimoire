namespace Grimoire.Mcp.Projections;

internal sealed record ListView
{
    public required IReadOnlyList<MemoryListEntry> Memories { get; init; }

    public required int Count { get; init; }
}
