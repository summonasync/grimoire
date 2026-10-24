namespace Grimoire.Infrastructure.Configuration;

public sealed record MemoryOptions
{
    public double SessionTtlHours { get; init; } = 24;

    public double DedupSimilarityThreshold { get; init; } = 0.92;

    public double RelatedSimilarityThreshold { get; init; } = 0.82;

    public double RecallForeignProjectFactor { get; init; } = 0.6;

    public RecencyHalfLifeOptions RecencyHalfLifeDays { get; init; } = new();

    public double TombstoneRetentionDays { get; init; } = 30;

    public int ReindexBatchSize { get; init; } = 64;
}
