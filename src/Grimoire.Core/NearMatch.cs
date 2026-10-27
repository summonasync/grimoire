namespace Grimoire.Core;

public sealed record NearMatch
{
    public required Guid MemoryId { get; init; }

    public required double Similarity { get; init; }

    public required string Snippet { get; init; }
}
