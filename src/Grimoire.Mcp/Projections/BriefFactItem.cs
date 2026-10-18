using Grimoire.Core;

namespace Grimoire.Mcp.Projections;

internal sealed record BriefFactItem
{
    public required Guid MemoryId { get; init; }

    public required string Content { get; init; }

    public required MemoryScope Scope { get; init; }

    public required double Score { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
