namespace Grimoire.Core;

public sealed record HistoryEntry
{
    public required Guid Id { get; init; }

    public required MemoryStatus Status { get; init; }

    public required string Tome { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? TombstonedAt { get; init; }

    public string? TombstoneReason { get; init; }

    public required MemoryActor Actor { get; init; }

    public required string BodyPreview { get; init; }
}
