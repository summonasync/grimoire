using System.Text.Json.Nodes;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Configuration;

public sealed class ProjectRegistryFileStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "grimoire-infra-tests", Guid.NewGuid().ToString("N"));

    private readonly string _configPath;

    private readonly ProjectRegistryFileStore _store;

    public ProjectRegistryFileStoreTests()
    {
        Directory.CreateDirectory(_directory);
        _configPath = Path.Combine(_directory, "config.json");
        _store = new ProjectRegistryFileStore(_configPath);
    }

    [Fact]
    public void Read_MissingConfig_ReturnsEmptyRegistry()
    {
        _store.Read().ShouldBeEmpty();
    }

    [Fact]
    public void Read_ParsesTheProjectsSectionCaseInsensitively()
    {
        File.WriteAllText(_configPath, """{ "projects": [ { "Alias": "grimoire", "Paths": ["/src/grimoire"] } ] }""");

        var records = _store.Read();

        var record = records.ShouldHaveSingleItem();
        record.Alias.ShouldBe("grimoire");
        record.Paths.ShouldBe(["/src/grimoire"]);
    }

    [Fact]
    public void Mutate_OnMissingConfig_CreatesTheFileWithDefaults()
    {
        _store.Mutate(records =>
        {
            records.Add(new ProjectAliasRecord { Alias = "fresh", Paths = ["/src/fresh"] });
            return "Added project 'fresh'.";
        });

        var options = new ConfigLoader(_configPath).Load();
        options.Projects.Single().Alias.ShouldBe("fresh");
        options.Qdrant.Collection.ShouldBe(new GrimoireOptions().Qdrant.Collection);
    }

    [Fact]
    public void Mutate_PreservesUnrelatedSections()
    {
        File.WriteAllText(_configPath, """{ "serve": { "port": 5080 }, "projects": [] }""");

        _store.Mutate(records =>
        {
            records.Add(new ProjectAliasRecord { Alias = "fresh", Paths = ["/src/fresh"] });
            return "Added project 'fresh'.";
        });

        JsonNode.Parse(File.ReadAllText(_configPath))!["serve"]!["port"]!.GetValue<int>().ShouldBe(5080);
    }

    [Fact]
    public void Mutate_InvalidRegistryAfterMutation_ThrowsAndLeavesTheFileUntouched()
    {
        File.WriteAllText(_configPath, """{ "projects": [ { "alias": "grimoire", "paths": ["/src/grimoire"] } ] }""");
        var original = File.ReadAllText(_configPath);

        Should.Throw<ProjectRegistryMutationException>(() => _store.Mutate(records =>
        {
            records.Add(new ProjectAliasRecord { Alias = "fresh", Paths = ["/src/grimoire"] });
            return "should never persist";
        })).Message.ShouldContain("already registered");

        File.ReadAllText(_configPath).ShouldBe(original);
    }

    [Fact]
    public void Mutate_AppliesTheLambdaAndPersists()
    {
        File.WriteAllText(_configPath, """{ "projects": [ { "alias": "grimoire", "paths": ["/src/grimoire"] } ] }""");

        _store.Mutate(records =>
        {
            records.RemoveAt(0);
            return "Removed project 'grimoire'.";
        });

        _store.Read().ShouldBeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
