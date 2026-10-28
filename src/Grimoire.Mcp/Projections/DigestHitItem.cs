using Grimoire.Core;

namespace Grimoire.Mcp.Projections;

internal sealed record DigestHitItem
{
    public required Guid MemoryId { get; init; }

    public required string Match { get; init; }

    public required string Content { get; init; }

    public required bool Truncated { get; init; }

    public double? Similarity { get; init; }

    public required MemoryScope Scope { get; init; }

    public required MemoryKind Kind { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string? TombstoneReason { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset LastAccessedAt { get; init; }
}
