namespace Grimoire.Core;

public sealed record VectorMatch
{
    public required MemoryRecord Record { get; init; }

    public required double Similarity { get; init; }
}
