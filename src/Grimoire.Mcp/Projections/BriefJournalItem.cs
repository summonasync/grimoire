using Grimoire.Core;

namespace Grimoire.Mcp.Projections;

internal sealed record BriefJournalItem
{
    public required Guid MemoryId { get; init; }

    public required string Content { get; init; }

    public required bool Truncated { get; init; }

    public required MemoryScope Scope { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
