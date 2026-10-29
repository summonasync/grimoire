using Grimoire.Core;
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
            NearDuplicateSimilarityThreshold = 0.85,
            DedupSimilarityThreshold = 0.8,
            RelatedSimilarityThreshold = 0.75,
            DigestSimilarityThreshold = 0.7,
            RecallForeignProjectFactor = 0.75,
            TombstoneRetentionDays = 14,
            ReindexBatchSize = 128,
            RecencyHalfLifeDays = new RecencyHalfLifeOptions { Session = 1, Project = 7, Global = 60, Personal = 120 },
        }, "ainora-luna");

        options.DefaultTome.ShouldBe("ainora-luna");
        options.SessionTtlHours.ShouldBe(6);
        options.NearDuplicateSimilarityThreshold.ShouldBe(0.85);
        options.DedupSimilarityThreshold.ShouldBe(0.8);
        options.RelatedSimilarityThreshold.ShouldBe(0.75);
        options.DigestSimilarityThreshold.ShouldBe(0.7);
        options.RecallForeignProjectFactor.ShouldBe(0.75);
        options.TombstoneRetentionDays.ShouldBe(14);
        options.ReindexBatchSize.ShouldBe(128);
        options.RecencyHalfLives.SessionDays.ShouldBe(1);
        options.RecencyHalfLives.ProjectDays.ShouldBe(7);
        options.RecencyHalfLives.GlobalDays.ShouldBe(60);
        options.RecencyHalfLives.PersonalDays.ShouldBe(120);
    }

    [Fact]
    public void ToEngineOptions_LeavesEngineOnlySectionsAtDefaults()
    {
        var options = EngineOptionsMapper.ToEngineOptions(new MemoryOptions(), TomeKey.DefaultTome);

        options.Compaction.MinSimilarity.ShouldBe(0.85);
        options.Compaction.StalenessDays.ShouldBe(7);
        options.RecallOversampling.ShouldBe(3);
        options.ReindexBatchSize.ShouldBe(64);
    }

    [Fact]
    public void ToEngineOptions_ClampsReindexBatchSizeToAtLeastOne()
    {
        var options = EngineOptionsMapper.ToEngineOptions(new MemoryOptions { ReindexBatchSize = 0 }, TomeKey.DefaultTome);

        options.ReindexBatchSize.ShouldBe(1);
    }
}
