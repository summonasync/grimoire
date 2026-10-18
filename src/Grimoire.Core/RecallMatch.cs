namespace Grimoire.Core;

public sealed record RecallMatch
{
    public required MemoryRecord Record { get; init; }

    public required double Similarity { get; init; }

    public required double Score { get; init; }
}
