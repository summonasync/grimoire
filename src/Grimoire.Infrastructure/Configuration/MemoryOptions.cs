namespace Grimoire.Infrastructure.Configuration;

public sealed record MemoryOptions
{
    public double SessionTtlHours { get; init; } = 24;

    public double DedupSimilarityThreshold { get; init; } = 0.92;

    public RecencyHalfLifeOptions RecencyHalfLifeDays { get; init; } = new();

    public double TombstoneRetentionDays { get; init; } = 30;
}
