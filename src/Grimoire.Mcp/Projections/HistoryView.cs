namespace Grimoire.Mcp.Projections;

internal sealed record HistoryView
{
    public required Guid MemoryId { get; init; }

    public required string Tome { get; init; }

    public required bool Found { get; init; }

    public required IReadOnlyList<HistoryEntryView> Chain { get; init; }
}
