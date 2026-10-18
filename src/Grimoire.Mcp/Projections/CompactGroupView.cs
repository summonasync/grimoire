namespace Grimoire.Mcp.Projections;

internal sealed record CompactGroupView
{
    public required IReadOnlyList<Guid> MemoryIds { get; init; }

    public required double Similarity { get; init; }

    public required DateTimeOffset StalestAccess { get; init; }

    public required IReadOnlyList<CompactEntryView> Entries { get; init; }
}
