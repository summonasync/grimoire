namespace Grimoire.Mcp.Projections;

internal sealed record NearDuplicateView
{
    public required Guid MemoryId { get; init; }

    public required double Similarity { get; init; }

    public required string Snippet { get; init; }
}
