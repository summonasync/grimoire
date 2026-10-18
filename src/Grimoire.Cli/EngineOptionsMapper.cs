using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;

namespace Grimoire.Cli;

internal static class EngineOptionsMapper
{
    public static MemoryEngineOptions ToEngineOptions(MemoryOptions options) => new()
    {
        SessionTtlHours = options.SessionTtlHours,
        DedupSimilarityThreshold = options.DedupSimilarityThreshold,
        RecencyHalfLives = new RecencyHalfLives
        {
            SessionDays = options.RecencyHalfLifeDays.Session,
            ProjectDays = options.RecencyHalfLifeDays.Project,
            GlobalDays = options.RecencyHalfLifeDays.Global,
            PersonalDays = options.RecencyHalfLifeDays.Personal,
        },
        TombstoneRetentionDays = options.TombstoneRetentionDays,
    };
}
