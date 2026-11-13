using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class EngineOptionsMapperTests : IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> EmptyEnvironment = new Dictionary<string, string>();

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "grimoire-cli-tests", "engine-options-mapper", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string WriteConfig(string json)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "config.json");
        File.WriteAllText(path, json);
        return path;
    }

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
            DailyTag = "field-notes",
            DailyTimeZone = "Europe/Moscow",
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
        options.DailyTag.ShouldBe("field-notes");
        options.DailyTimeZone.ShouldBe("Europe/Moscow");
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
    public void ToEngineOptions_KeepsDailyDefaultsWhenTheKeysAreAbsent()
    {
        var options = EngineOptionsMapper.ToEngineOptions(new MemoryOptions(), TomeKey.DefaultTome);

        options.DailyTag.ShouldBe("daily-notes");
        options.DailyTimeZone.ShouldBeNull();
    }

    [Fact]
    public void ToEngineOptions_UnsetDailyTimeZoneStaysNullBecauseUtcWouldBeAConfiguredChoice()
    {
        var options = EngineOptionsMapper.ToEngineOptions(new MemoryOptions(), TomeKey.DefaultTome);

        options.DailyTimeZone.ShouldBeNull();

        var zone = DailyWindowResolver.ResolveZone(options.DailyTimeZone);
        zone.Source.ShouldBe(DailyZoneSource.MachineLocal);
    }

    [Fact]
    public void ToEngineOptions_ExplicitDailyValuesWinOverTheDefaults()
    {
        var options = EngineOptionsMapper.ToEngineOptions(
            new MemoryOptions { DailyTag = "field-notes", DailyTimeZone = "Europe/Moscow" },
            TomeKey.DefaultTome);

        options.DailyTag.ShouldBe("field-notes");
        options.DailyTimeZone.ShouldBe("Europe/Moscow");
        DailyWindowResolver.ResolveZone(options.DailyTimeZone).Zone.BaseUtcOffset.ShouldBe(TimeSpan.FromHours(3));
    }

    [Fact]
    public void ToEngineOptions_UnknownZoneIdReachesTheResolverAndFallsBackToUtcWithoutThrowing()
    {
        var options = EngineOptionsMapper.ToEngineOptions(new MemoryOptions { DailyTimeZone = "Not/AZone" }, TomeKey.DefaultTome);

        var zone = DailyWindowResolver.ResolveZone(options.DailyTimeZone);

        zone.Source.ShouldBe(DailyZoneSource.UtcFallback);
        zone.Zone.ShouldBeSameAs(TimeZoneInfo.Utc);
    }

    [Fact]
    public void Load_DailyKeysInAConfigFile_AreAcceptedByTheStrictBinder()
    {
        var path = WriteConfig("""{ "memory": { "dailyTag": "field-notes", "dailyTimeZone": "Not/AZone" } }""");

        var config = new ConfigLoader(path, EmptyEnvironment).Load();
        var options = EngineOptionsMapper.ToEngineOptions(config.Memory, config.DefaultTome);

        options.DailyTag.ShouldBe("field-notes");
        options.DailyTimeZone.ShouldBe("Not/AZone");
    }

    [Fact]
    public void Load_ConfigWithoutTheDailyKeys_LeavesTheTimeZoneUnsetRatherThanUtc()
    {
        var path = WriteConfig("""{ "memory": { "dailyTag": "field-notes" } }""");

        var config = new ConfigLoader(path, EmptyEnvironment).Load();
        var options = EngineOptionsMapper.ToEngineOptions(config.Memory, config.DefaultTome);

        options.DailyTag.ShouldBe("field-notes");
        options.DailyTimeZone.ShouldBeNull();
        DailyWindowResolver.ResolveZone(options.DailyTimeZone).Source.ShouldBe(DailyZoneSource.MachineLocal);
    }

    [Fact]
    public void Load_UnknownMemoryKey_IsRejectedSoTheTwoDailyKeysBeingAcceptedIsMeaningful()
    {
        var path = WriteConfig("""{ "memory": { "dailyTagz": "field-notes" } }""");

        Should.Throw<ConfigException>(() => new ConfigLoader(path, EmptyEnvironment).Load());
    }

    [Fact]
    public void Load_DailyKeysFromTheEnvironment_ReachTheEngineOptions()
    {
        var environment = new Dictionary<string, string>
        {
            ["GRIMOIRE_MEMORY__DAILYTAG"] = "field-notes",
            ["GRIMOIRE_MEMORY__DAILYTIMEZONE"] = "Europe/Moscow",
        };

        var config = new ConfigLoader(Path.Combine(_directory, "missing", "config.json"), environment).Load();
        var options = EngineOptionsMapper.ToEngineOptions(config.Memory, config.DefaultTome);

        options.DailyTag.ShouldBe("field-notes");
        options.DailyTimeZone.ShouldBe("Europe/Moscow");
        DailyWindowResolver.ResolveZone(options.DailyTimeZone).Zone.BaseUtcOffset.ShouldBe(TimeSpan.FromHours(3));
    }

    [Fact]
    public void ToEngineOptions_ClampsReindexBatchSizeToAtLeastOne()
    {
        var options = EngineOptionsMapper.ToEngineOptions(new MemoryOptions { ReindexBatchSize = 0 }, TomeKey.DefaultTome);

        options.ReindexBatchSize.ShouldBe(1);
    }
}
