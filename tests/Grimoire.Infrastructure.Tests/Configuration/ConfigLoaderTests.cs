using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Configuration;

public sealed class ConfigLoaderTests : IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> EmptyEnvironment = new Dictionary<string, string>();

    private readonly string _directory;

    public ConfigLoaderTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "grimoire-config-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Load_WithoutFileOrEnvironment_ReturnsSpecDefaults()
    {
        var options = new ConfigLoader(MissingPath(), EmptyEnvironment).Load();

        options.Qdrant.Endpoint.ShouldBe("http://localhost:6334");
        options.Qdrant.ApiKey.ShouldBeNull();
        options.Qdrant.Collection.ShouldBe("grimoire");
        options.Embedding.Provider.ShouldBe("ollama");
        options.Embedding.Model.ShouldBe("qwen3-embedding");
        options.Embedding.Endpoint.ShouldBe("http://localhost:11434");
        options.Embedding.Dimensions.ShouldBe(1024);
        options.Memory.SessionTtlHours.ShouldBe(24);
        options.Memory.DedupSimilarityThreshold.ShouldBe(0.92);
        options.Memory.RelatedSimilarityThreshold.ShouldBe(0.82);
        options.Memory.DigestSimilarityThreshold.ShouldBe(0.6);
        options.Memory.RecallForeignProjectFactor.ShouldBe(0.6);
        options.Memory.RecencyHalfLifeDays.Session.ShouldBe(0.5);
        options.Memory.RecencyHalfLifeDays.Project.ShouldBe(30);
        options.Memory.RecencyHalfLifeDays.Global.ShouldBe(90);
        options.Memory.RecencyHalfLifeDays.Personal.ShouldBe(180);
        options.Memory.TombstoneRetentionDays.ShouldBe(30);
        options.DefaultTome.ShouldBe(TomeKey.DefaultTome);
        options.Serve.Port.ShouldBe(4770);
        options.Serve.GcIntervalMinutes.ShouldBe(30);
        options.Sessions.RetentionHours.ShouldBeNull();
        options.SessionsRetentionHoursOrDefault.ShouldBe(24);
        options.Projects.ShouldBeEmpty();
    }

    [Fact]
    public void Load_GrimoireHomeVariable_IsNotTreatedAsAConfigOverride()
    {
        var environment = new Dictionary<string, string> { ["GRIMOIRE_HOME"] = "/tmp/some-home" };

        var options = new ConfigLoader(MissingPath(), environment).Load();

        options.DefaultTome.ShouldBe(TomeKey.DefaultTome);
    }

    [Fact]
    public void Load_WithPartialFile_MergesOverDefaults()
    {
        var path = WriteConfig("""
            {
              "defaultTome": "ainora-luna",
              "qdrant": { "endpoint": "http://10.0.0.8:6334" },
              "serve": { "port": 5001 }
            }
            """);

        var options = new ConfigLoader(path, EmptyEnvironment).Load();

        options.DefaultTome.ShouldBe("ainora-luna");
        options.Qdrant.Endpoint.ShouldBe("http://10.0.0.8:6334");
        options.Qdrant.Collection.ShouldBe("grimoire");
        options.Serve.Port.ShouldBe(5001);
    }

    [Fact]
    public void Load_WithDigestSimilarityThreshold_RoundTripsValue()
    {
        var path = WriteConfig("""{ "memory": { "digestSimilarityThreshold": 0.55 } }""");

        var options = new ConfigLoader(path, EmptyEnvironment).Load();

        options.Memory.DigestSimilarityThreshold.ShouldBe(0.55);
    }

    [Fact]
    public void Load_WithEnvironmentTomeOverride_AppliesDefaultTome()
    {
        var loader = Loader(MissingPath(), [("GRIMOIRE_DEFAULTTOME", "team-platform")]);

        var options = loader.Load();

        options.DefaultTome.ShouldBe("team-platform");
    }

    [Fact]
    public void Load_WithReservedTomeVariable_IsNotAConfigurationOverride()
    {
        var loader = Loader(MissingPath(), [("GRIMOIRE_TOME", "session-tome"), ("GRIMOIRE_SERVE__PORT", "6000")]);

        var options = loader.Load();

        options.Serve.Port.ShouldBe(6000);
        options.DefaultTome.ShouldBe(TomeKey.DefaultTome);
    }

    [Fact]
    public void Load_WithUnknownFileProperty_Throws()
    {
        var path = WriteConfig("""{ "qdrant": { "endpiont": "x" } }""");

        var exception = Should.Throw<ConfigException>(() => new ConfigLoader(path, EmptyEnvironment).Load());

        exception.Message.ShouldContain("endpiont");
    }

    [Fact]
    public void Load_WithInvalidJson_Throws()
    {
        var path = WriteConfig("{ not json");

        Should.Throw<ConfigException>(() => new ConfigLoader(path, EmptyEnvironment).Load());
    }

    [Fact]
    public void Load_WhenEnvironmentAndFileCollide_EnvironmentWins()
    {
        var path = WriteConfig("""{ "serve": { "port": 5001 } }""");
        var loader = Loader(path, [("GRIMOIRE_SERVE__PORT", "6000")]);

        var options = loader.Load();

        options.Serve.Port.ShouldBe(6000);
    }

    [Fact]
    public void Load_WithEnvironmentOnlyOverride_AppliesValue()
    {
        var loader = Loader(MissingPath(), [("GRIMOIRE_QDRANT__ENDPOINT", "http://qdrant.internal:6334")]);

        var options = loader.Load();

        options.Qdrant.Endpoint.ShouldBe("http://qdrant.internal:6334");
    }

    [Fact]
    public void Load_WithNestedEnvironmentOverride_AppliesToScopeHalfLife()
    {
        var loader = Loader(MissingPath(), [("GRIMOIRE_MEMORY__RECENCYHALFLIFEDAYS__SESSION", "2")]);

        var options = loader.Load();

        options.Memory.RecencyHalfLifeDays.Session.ShouldBe(2);
        options.Memory.RecencyHalfLifeDays.Global.ShouldBe(90);
    }

    [Fact]
    public void Load_WithEnvironmentOverride_AppliesRelatedSimilarityThreshold()
    {
        var loader = Loader(MissingPath(), [("GRIMOIRE_MEMORY__RELATEDSIMILARITYTHRESHOLD", "0.75")]);

        var options = loader.Load();

        options.Memory.RelatedSimilarityThreshold.ShouldBe(0.75);
        options.Memory.DedupSimilarityThreshold.ShouldBe(0.92);
    }

    [Fact]
    public void Load_WithNullableStringEnvironmentOverride_SetsApiKey()
    {
        var loader = Loader(MissingPath(), [("GRIMOIRE_QDRANT__APIKEY", "secret")]);

        var options = loader.Load();

        options.Qdrant.ApiKey.ShouldBe("secret");
    }

    [Fact]
    public void Load_WithNumericLookingApiKeyOverride_StaysAString()
    {
        var loader = Loader(MissingPath(), [("GRIMOIRE_QDRANT__APIKEY", "12345")]);

        var options = loader.Load();

        options.Qdrant.ApiKey.ShouldBe("12345");
    }

    [Fact]
    public void Load_WithUnknownEnvironmentProperty_Throws()
    {
        var loader = Loader(MissingPath(), [("GRIMOIRE_SERVE__PORTT", "6000")]);

        var exception = Should.Throw<ConfigException>(() => loader.Load());

        exception.Message.ShouldContain("GRIMOIRE_SERVE__PORTT");
    }

    [Fact]
    public void Load_WithReservedActorVariable_IsNotAConfigurationOverride()
    {
        var loader = Loader(MissingPath(), [("GRIMOIRE_ACTOR", "dan+claude"), ("GRIMOIRE_SERVE__PORT", "6000")]);

        var options = loader.Load();

        options.Serve.Port.ShouldBe(6000);
    }

    [Fact]
    public void Load_WithNonNumericEnvironmentOverride_Throws()
    {
        var loader = Loader(MissingPath(), [("GRIMOIRE_SERVE__PORT", "loopback")]);

        var exception = Should.Throw<ConfigException>(() => loader.Load());

        exception.Message.ShouldContain("number");
    }

    [Fact]
    public void Load_WithExplicitNullSection_Throws()
    {
        var path = WriteConfig("""{ "serve": null }""");

        Should.Throw<ConfigException>(() => new ConfigLoader(path, EmptyEnvironment).Load());
    }

    [Fact]
    public void Load_WithExplicitNullForNonNullableOption_Throws()
    {
        var path = WriteConfig("""{ "qdrant": { "collection": null } }""");

        Should.Throw<ConfigException>(() => new ConfigLoader(path, EmptyEnvironment).Load());
    }

    [Fact]
    public void Load_WithExplicitNullForNullableOption_IsAccepted()
    {
        var path = WriteConfig("""{ "qdrant": { "apiKey": null } }""");

        var options = new ConfigLoader(path, EmptyEnvironment).Load();

        options.Qdrant.ApiKey.ShouldBeNull();
    }

    [Fact]
    public void Load_WithSessionsRetentionHours_RoundTripsConfiguredValue()
    {
        var path = WriteConfig("""{ "memory": { "sessionTtlHours": 12 }, "sessions": { "retentionHours": 48 } }""");

        var options = new ConfigLoader(path, EmptyEnvironment).Load();

        options.Sessions.RetentionHours.ShouldBe(48);
        options.SessionsRetentionHoursOrDefault.ShouldBe(48);
    }

    [Fact]
    public void Load_WithoutSessionsRetentionHours_FallsBackToMemorySessionTtl()
    {
        var path = WriteConfig("""{ "memory": { "sessionTtlHours": 6 } }""");

        var options = new ConfigLoader(path, EmptyEnvironment).Load();

        options.Sessions.RetentionHours.ShouldBeNull();
        options.SessionsRetentionHoursOrDefault.ShouldBe(6);
    }

    [Fact]
    public void Load_WithNullableNumberEnvironmentOverride_AppliesSessionsRetentionHours()
    {
        var loader = Loader(MissingPath(), [("GRIMOIRE_SESSIONS__RETENTIONHOURS", "36")]);

        var options = loader.Load();

        options.Sessions.RetentionHours.ShouldBe(36);
        options.SessionsRetentionHoursOrDefault.ShouldBe(36);
    }

    [Fact]
    public void Load_WithProjectsSection_ParsesRecords()
    {
        var path = WriteConfig("""
            {
              "projects": [
                {
                  "alias": "grimoire",
                  "paths": ["/home/user/grimoire", "/home/user/grimoire-fork"],
                  "boardProjectId": "7450aeab-137e-446d-88ed-5ffc9b624a1b",
                  "displayName": "Grimoire"
                },
                { "alias": "squeezr", "paths": [] }
              ]
            }
            """);

        var options = new ConfigLoader(path, EmptyEnvironment).Load();

        options.Projects.Count.ShouldBe(2);
        options.Projects[0].Alias.ShouldBe("grimoire");
        options.Projects[0].Paths.ShouldBe(["/home/user/grimoire", "/home/user/grimoire-fork"]);
        options.Projects[0].BoardProjectId.ShouldBe("7450aeab-137e-446d-88ed-5ffc9b624a1b");
        options.Projects[0].DisplayName.ShouldBe("Grimoire");
        options.Projects[1].Alias.ShouldBe("squeezr");
        options.Projects[1].Paths.ShouldBeEmpty();
        options.Projects[1].BoardProjectId.ShouldBeNull();
        options.Projects[1].DisplayName.ShouldBeNull();
    }

    [Fact]
    public void Load_WithInvalidProjectAlias_Throws()
    {
        var path = WriteConfig("""{ "projects": [ { "alias": "Grimoire", "paths": ["/home/user/grimoire"] } ] }""");

        var exception = Should.Throw<ConfigException>(() => new ConfigLoader(path, EmptyEnvironment).Load());

        exception.Message.ShouldContain("Grimoire");
        exception.Message.ShouldContain("kebab-case");
    }

    [Fact]
    public void Load_WithProjectMissingAlias_Throws()
    {
        var path = WriteConfig("""{ "projects": [ { "paths": ["/home/user/grimoire"] } ] }""");

        var exception = Should.Throw<ConfigException>(() => new ConfigLoader(path, EmptyEnvironment).Load());

        exception.Message.ShouldContain("alias");
    }

    [Fact]
    public void Load_WithDuplicateProjectAliases_Throws()
    {
        var path = WriteConfig("""
            {
              "projects": [
                { "alias": "grimoire", "paths": ["/home/user/grimoire"] },
                { "alias": "grimoire", "paths": ["/home/user/grimoire-fork"] }
              ]
            }
            """);

        var exception = Should.Throw<ConfigException>(() => new ConfigLoader(path, EmptyEnvironment).Load());

        exception.Message.ShouldContain("alias 'grimoire' is already registered");
    }

    [Fact]
    public void Load_WithDuplicateProjectPaths_Throws()
    {
        var path = WriteConfig("""
            {
              "projects": [
                { "alias": "grimoire", "paths": ["/home/user/grimoire"] },
                { "alias": "grimoire-clone", "paths": ["/home/user/grimoire/"] }
              ]
            }
            """);

        var exception = Should.Throw<ConfigException>(() => new ConfigLoader(path, EmptyEnvironment).Load());

        exception.Message.ShouldContain("already registered");
    }

    [Fact]
    public void Load_WithUnknownProjectProperty_Throws()
    {
        var path = WriteConfig("""{ "projects": [ { "alias": "grimoire", "pathz": ["/x"] } ] }""");

        var exception = Should.Throw<ConfigException>(() => new ConfigLoader(path, EmptyEnvironment).Load());

        exception.Message.ShouldContain("pathz");
    }

    private static ConfigLoader Loader(
        string path,
        IEnumerable<(string Name, string Value)> overrides) =>
        new(path, overrides.ToDictionary(entry => entry.Name, entry => entry.Value));

    private string MissingPath() => Path.Combine(_directory, "missing", "config.json");

    private string WriteConfig(string json)
    {
        var path = Path.Combine(_directory, $"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
