namespace Grimoire.Core;

public sealed record CompactionOptions
{
    public double MinSimilarity { get; init; } = 0.85;

    public double StalenessDays { get; init; } = 7;

    public int MinClusterSize { get; init; } = 2;
}
