using Grimoire.Core;

namespace Grimoire.Api.Contracts;

public sealed record EngineSettings(
    double SessionTtlHours,
    double DedupSimilarityThreshold,
    double RelatedSimilarityThreshold,
    double TombstoneRetentionDays,
    int RecallOversampling,
    RecencyHalfLives RecencyHalfLives,
    CompactionOptions Compaction,
    BriefSettings Brief);
