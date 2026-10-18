namespace Grimoire.Core;

public sealed record MemoryEngineOptions
{
    public string DefaultTome { get; init; } = TomeKey.DefaultTome;

    public double SessionTtlHours { get; init; } = 24;

    public double DedupSimilarityThreshold { get; init; } = 0.92;

    public RecencyHalfLives RecencyHalfLives { get; init; } = new();

    public double TombstoneRetentionDays { get; init; } = 30;

    public CompactionOptions Compaction { get; init; } = new();

    public int RecallOversampling { get; init; } = 3;

    public double IdempotencyWindowHours { get; init; } = 24;

    public int BriefMaxTotalChars { get; init; } = 8000;

    public int BriefMaxEntryChars { get; init; } = 400;
}
