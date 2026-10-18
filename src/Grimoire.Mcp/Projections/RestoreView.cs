namespace Grimoire.Mcp.Projections;

internal sealed record RestoreView
{
    public required Guid MemoryId { get; init; }

    public required string Tome { get; init; }

    public required bool Restored { get; init; }

    public string? Reason { get; init; }

    public SupersederView? Superseder { get; init; }
}
