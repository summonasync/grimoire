namespace Grimoire.Core;

public sealed record MemoryRecord
{
    public required Guid Id { get; init; }

    public required string Content { get; init; }

    public required MemoryScope Scope { get; init; }

    public required MemoryKind Kind { get; init; }

    public required MemoryActor Actor { get; init; }

    public string? ProjectId { get; init; }

    public string? SessionId { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public double Importance { get; init; } = 0.5;

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset LastAccessedAt { get; init; }

    public long AccessCount { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public MemoryStatus Status { get; init; } = MemoryStatus.Active;

    public Guid? SupersededBy { get; init; }

    public string? TombstoneReason { get; init; }

    public DateTimeOffset? TombstonedAt { get; init; }

    public IReadOnlyList<Guid> Sources { get; init; } = [];

    public string? IdempotencyKey { get; init; }
}
