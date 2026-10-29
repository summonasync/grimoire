using Grimoire.Core;

namespace Grimoire.Api.Contracts;

public sealed record EngineSettings(
    double SessionTtlHours,
    double NearDuplicateSimilarityThreshold,
    double DedupSimilarityThreshold,
    double RelatedSimilarityThreshold,
    double DigestSimilarityThreshold,
    double TombstoneRetentionDays,
    int RecallOversampling,
    RecencyHalfLives RecencyHalfLives,
    CompactionOptions Compaction,
    BriefSettings Brief);
