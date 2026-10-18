using Grimoire.Core;

namespace Grimoire.Mcp.Projections;

internal sealed record HistoryEntryView
{
    public required Guid MemoryId { get; init; }

    public required MemoryStatus Status { get; init; }

    public required string Tome { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? TombstonedAt { get; init; }

    public string? TombstoneReason { get; init; }

    public required string Actor { get; init; }

    public required string BodyPreview { get; init; }
}
