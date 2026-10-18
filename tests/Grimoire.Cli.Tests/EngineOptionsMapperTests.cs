using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class EngineOptionsMapperTests
{
    [Fact]
    public void ToEngineOptions_MapsEveryConfiguredValue()
    {
        var options = EngineOptionsMapper.ToEngineOptions(new MemoryOptions
        {
            SessionTtlHours = 6,
            DedupSimilarityThreshold = 0.8,
            TombstoneRetentionDays = 14,
            RecencyHalfLifeDays = new RecencyHalfLifeOptions { Session = 1, Project = 7, Global = 60, Personal = 120 },
        });

        options.SessionTtlHours.ShouldBe(6);
        options.DedupSimilarityThreshold.ShouldBe(0.8);
        options.TombstoneRetentionDays.ShouldBe(14);
        options.RecencyHalfLives.SessionDays.ShouldBe(1);
        options.RecencyHalfLives.ProjectDays.ShouldBe(7);
        options.RecencyHalfLives.GlobalDays.ShouldBe(60);
        options.RecencyHalfLives.PersonalDays.ShouldBe(120);
    }

    [Fact]
    public void ToEngineOptions_LeavesEngineOnlySectionsAtDefaults()
    {
        var options = EngineOptionsMapper.ToEngineOptions(new MemoryOptions());

        options.Compaction.MinSimilarity.ShouldBe(0.85);
        options.Compaction.StalenessDays.ShouldBe(7);
        options.RecallOversampling.ShouldBe(3);
    }
}
