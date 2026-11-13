namespace Grimoire.Core;

public sealed record MemoryEngineOptions
{
    public const int DefaultBriefDigestExcerptChars = 80;

    public const int DefaultBriefNeverTruncateChars = 300;

    public string DefaultTome { get; init; } = TomeKey.DefaultTome;

    public double SessionTtlHours { get; init; } = 24;

    public double NearDuplicateSimilarityThreshold { get; init; } = 0.80;

    public double DedupSimilarityThreshold { get; init; } = 0.92;

    public double NearTopicSimilarityThreshold { get; init; } = 0.75;

    public int TopicAgreementCount { get; init; } = 2;

    public double TopicConfidenceThreshold { get; init; } = 0.8;

    public double RelatedSimilarityThreshold { get; init; } = 0.82;

    public RecencyHalfLives RecencyHalfLives { get; init; } = new();

    public double TombstoneRetentionDays { get; init; } = 30;

    public CompactionOptions Compaction { get; init; } = new();

    public int RecallOversampling { get; init; } = 3;

    public double RecallForeignProjectFactor { get; init; } = 0.6;

    public double IdempotencyWindowHours { get; init; } = 24;

    public int BriefMaxTotalChars { get; init; } = 8000;

    public int BriefMaxEntryChars { get; init; } = 400;

    public int BriefDigestExcerptChars { get; init; } = DefaultBriefDigestExcerptChars;

    public int BriefNeverTruncateChars { get; init; } = DefaultBriefNeverTruncateChars;

    public string DailyTag { get; init; } = "daily-notes";

    public int DailyLookbackDays { get; init; } = 7;

    public string? DailyTimeZone { get; init; }

    public int BriefDailyMaxEntryChars { get; init; } = 1500;

    public int BriefDailyMaxTotalChars { get; init; } = 6000;

    public double DigestSimilarityThreshold { get; init; } = 0.60;

    public int DigestRecentCap { get; init; } = 10;

    public int DigestGoneCap { get; init; } = 10;

    public int DigestMaxTotalChars { get; init; } = 6000;

    public int DigestMaxEntryChars { get; init; } = 400;

    public int ReindexBatchSize { get; init; } = 64;
}
