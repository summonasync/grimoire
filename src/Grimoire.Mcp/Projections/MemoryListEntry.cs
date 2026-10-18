using Grimoire.Core;

namespace Grimoire.Mcp.Projections;

internal sealed record MemoryListEntry
{
    public required Guid MemoryId { get; init; }

    public required string Content { get; init; }

    public required MemoryScope Scope { get; init; }

    public required MemoryKind Kind { get; init; }

    public required MemoryStatus Status { get; init; }

    public required string Actor { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string? ProjectId { get; init; }

    public string? SessionId { get; init; }

    public double Importance { get; init; }

    public long AccessCount { get; init; }

    public Guid? SupersededBy { get; init; }

    public string? TombstoneReason { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset LastAccessedAt { get; init; }

    public required int BodyChars { get; init; }
}
