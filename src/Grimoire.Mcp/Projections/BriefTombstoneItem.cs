namespace Grimoire.Mcp.Projections;

internal sealed record BriefTombstoneItem
{
    public required Guid MemoryId { get; init; }

    public required string? Reason { get; init; }

    public required DateTimeOffset TombstonedAt { get; init; }
}
