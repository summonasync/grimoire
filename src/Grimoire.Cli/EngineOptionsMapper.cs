using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;

namespace Grimoire.Cli;

internal static class EngineOptionsMapper
{
    public static MemoryEngineOptions ToEngineOptions(MemoryOptions options, string defaultTome) => new()
    {
        DefaultTome = defaultTome,
        SessionTtlHours = options.SessionTtlHours,
        NearDuplicateSimilarityThreshold = options.NearDuplicateSimilarityThreshold,
        DedupSimilarityThreshold = options.DedupSimilarityThreshold,
        RelatedSimilarityThreshold = options.RelatedSimilarityThreshold,
        DigestSimilarityThreshold = options.DigestSimilarityThreshold,
        RecallForeignProjectFactor = options.RecallForeignProjectFactor,
        RecencyHalfLives = new RecencyHalfLives
        {
            SessionDays = options.RecencyHalfLifeDays.Session,
            ProjectDays = options.RecencyHalfLifeDays.Project,
            GlobalDays = options.RecencyHalfLifeDays.Global,
            PersonalDays = options.RecencyHalfLifeDays.Personal,
        },
        TombstoneRetentionDays = options.TombstoneRetentionDays,
        DailyTag = options.DailyTag,
        DailyTimeZone = options.DailyTimeZone,
        ReindexBatchSize = Math.Max(1, options.ReindexBatchSize),
    };
}
