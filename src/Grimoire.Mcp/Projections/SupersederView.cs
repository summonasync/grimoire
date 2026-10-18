namespace Grimoire.Mcp.Projections;

internal sealed record SupersederView
{
    public required Guid MemoryId { get; init; }

    public required bool Tombstoned { get; init; }

    public string? TombstoneReason { get; init; }
}
