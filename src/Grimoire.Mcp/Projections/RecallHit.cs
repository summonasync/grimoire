using Grimoire.Core;

namespace Grimoire.Mcp.Projections;

internal sealed record RecallHit
{
    public required Guid MemoryId { get; init; }

    public required string Content { get; init; }

    public required MemoryScope Scope { get; init; }

    public required MemoryKind Kind { get; init; }

    public required string Actor { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string? ProjectId { get; init; }

    public string? SessionId { get; init; }

    public required double Similarity { get; init; }

    public required double Score { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset LastAccessedAt { get; init; }
}
