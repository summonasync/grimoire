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
        options.Embedding.Model.ShouldBe("nomic-embed-text");
        options.Embedding.Endpoint.ShouldBe("http://localhost:11434");
        options.Embedding.Dimensions.ShouldBe(768);
        options.Memory.SessionTtlHours.ShouldBe(24);
        options.Memory.DedupSimilarityThreshold.ShouldBe(0.92);
        options.Memory.RecencyHalfLifeDays.Session.ShouldBe(0.5);
        options.Memory.RecencyHalfLifeDays.Project.ShouldBe(30);
        options.Memory.RecencyHalfLifeDays.Global.ShouldBe(90);
        options.Memory.RecencyHalfLifeDays.Personal.ShouldBe(180);
        options.Memory.TombstoneRetentionDays.ShouldBe(30);
        options.Serve.Port.ShouldBe(4770);
        options.Serve.GcIntervalMinutes.ShouldBe(30);
    }

    [Fact]
    public void Load_WithPartialFile_MergesOverDefaults()
    {
        var path = WriteConfig("""
            {
              "qdrant": { "endpoint": "http://10.0.0.8:6334" },
              "serve": { "port": 5001 }
            }
            """);

        var options = new ConfigLoader(path, EmptyEnvironment).Load();

        options.Qdrant.Endpoint.ShouldBe("http://10.0.0.8:6334");
        options.Qdrant.Collection.ShouldBe("grimoire");
        options.Serve.Port.ShouldBe(5001);
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
    public void Load_WithNullableStringEnvironmentOverride_SetsApiKey()
    {
        var loader = Loader(MissingPath(), [("GRIMOIRE_QDRANT__APIKEY", "secret")]);

        var options = loader.Load();

        options.Qdrant.ApiKey.ShouldBe("secret");
    }

    [Fact]
    public void Load_WithUnknownEnvironmentProperty_Throws()
    {
        var loader = Loader(MissingPath(), [("GRIMOIRE_SERVE__PORTT", "6000")]);

        var exception = Should.Throw<ConfigException>(() => loader.Load());

        exception.Message.ShouldContain("GRIMOIRE_SERVE__PORTT");
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
