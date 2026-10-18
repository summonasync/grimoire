namespace Grimoire.Core;

public sealed record MemoryEngineOptions
{
    public double SessionTtlHours { get; init; } = 24;

    public double DedupSimilarityThreshold { get; init; } = 0.92;

    public RecencyHalfLives RecencyHalfLives { get; init; } = new();

    public double TombstoneRetentionDays { get; init; } = 30;

    public CompactionOptions Compaction { get; init; } = new();

    public int RecallOversampling { get; init; } = 3;
}
