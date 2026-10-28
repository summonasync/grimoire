namespace Grimoire.Core;

public sealed record MemoryEngineOptions
{
    public string DefaultTome { get; init; } = TomeKey.DefaultTome;

    public double SessionTtlHours { get; init; } = 24;

    public double NearDuplicateSimilarityThreshold { get; init; } = 0.80;

    public double DedupSimilarityThreshold { get; init; } = 0.92;

    public double NearTopicSimilarityThreshold { get; init; } = 0.75;

    public int TopicAgreementCount { get; init; } = 2;

    public double RelatedSimilarityThreshold { get; init; } = 0.82;

    public RecencyHalfLives RecencyHalfLives { get; init; } = new();

    public double TombstoneRetentionDays { get; init; } = 30;

    public CompactionOptions Compaction { get; init; } = new();

    public int RecallOversampling { get; init; } = 3;

    public double RecallForeignProjectFactor { get; init; } = 0.6;

    public double IdempotencyWindowHours { get; init; } = 24;

    public int BriefMaxTotalChars { get; init; } = 8000;

    public int BriefMaxEntryChars { get; init; } = 400;

    public double DigestSimilarityThreshold { get; init; } = 0.75;

    public int DigestRecentCap { get; init; } = 10;

    public int DigestGoneCap { get; init; } = 10;

    public int DigestMaxTotalChars { get; init; } = 6000;

    public int DigestMaxEntryChars { get; init; } = 400;

    public int ReindexBatchSize { get; init; } = 64;
}
