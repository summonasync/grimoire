namespace Grimoire.Mcp.Projections;

internal sealed record CompactEntryView
{
    public required Guid MemoryId { get; init; }

    public required string Content { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset LastAccessedAt { get; init; }
}
