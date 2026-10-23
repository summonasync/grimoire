using System.Text.Json.Nodes;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class ProjectsCommandTests : IDisposable
{
    private const string SeedConfig = """
        {
          "serve": { "port": 5080 },
          "projects": [
            { "alias": "grimoire", "paths": ["/home/u/grimoire"], "boardProjectId": "7450", "displayName": "Grimoire" },
            { "alias": "squeezr", "paths": ["/home/u/squeezr", "/home/u/squeezr-wt"] }
          ]
        }
        """;

    private readonly TestHost _host = new();

    [Fact]
    public async Task List_PrintsAliasBoardNameAndPaths()
    {
        Seed(SeedConfig);

        var code = await _host.RunAsync("projects", "list");

        code.ShouldBe(ExitCodes.Ok);
        var output = _host.Output.ToString();
        output.ShouldContain("display name");
        output.ShouldContain("grimoire");
        output.ShouldContain("7450");
        output.ShouldContain("/home/u/squeezr, /home/u/squeezr-wt");
    }

    [Fact]
    public async Task List_WithoutProjectsSection_PrintsPlaceholder()
    {
        Seed("{}");

        var code = await _host.RunAsync("projects", "list");

        code.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain("No projects registered.");
    }

    [Fact]
    public async Task List_IsTheDefaultAction()
    {
        Seed(SeedConfig);

        var code = await _host.RunAsync("projects");

        code.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain("grimoire");
    }

    [Fact]
    public async Task Add_CreatesRecordAndPersistsRegistry()
    {
        Seed(SeedConfig);

        var code = await _host.RunAsync(
            "projects", "add", "fresh", "/home/u/fresh", "--display-name", "Fresh Project", "--board-project-id", "abc123");

        code.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain("Added project 'fresh'");
        var registry = LoadRegistry();
        var fresh = registry.Single(record => record.Alias == "fresh");
        fresh.Paths.ShouldBe(["/home/u/fresh"]);
        fresh.DisplayName.ShouldBe("Fresh Project");
        fresh.BoardProjectId.ShouldBe("abc123");
    }

    [Fact]
    public async Task Add_MultiplePaths_PersistsAll()
    {
        Seed(SeedConfig);

        var code = await _host.RunAsync("projects", "add", "multi", "/home/u/multi", "/home/u/multi-wt");

        code.ShouldBe(ExitCodes.Ok);
        LoadRegistry().Single(record => record.Alias == "multi").Paths
            .ShouldBe(["/home/u/multi", "/home/u/multi-wt"]);
    }

    [Fact]
    public async Task Add_KeepsUnrelatedSections()
    {
        Seed(SeedConfig);

        await _host.RunAsync("projects", "add", "fresh", "/home/u/fresh");

        JsonNode.Parse(File.ReadAllText(_host.ConfigPath))!["serve"]!["port"]!.GetValue<int>().ShouldBe(5080);
    }

    [Fact]
    public async Task Add_InvalidAlias_IsUsageAndKeepsFile()
    {
        Seed(SeedConfig);
        var original = File.ReadAllText(_host.ConfigPath);

        var code = await _host.RunAsync("projects", "add", "Slurpy.AI", "/home/u/slurpy");

        code.ShouldBe(ExitCodes.Usage);
        _host.Output.ToString().ShouldContain("kebab-case");
        File.ReadAllText(_host.ConfigPath).ShouldBe(original);
    }

    [Fact]
    public async Task Add_MissingPath_IsUsage()
    {
        Seed(SeedConfig);

        var code = await _host.RunAsync("projects", "add", "lonely");

        code.ShouldBe(ExitCodes.Usage);
    }

    [Fact]
    public async Task Add_RelativePath_IsUsage()
    {
        Seed(SeedConfig);

        var code = await _host.RunAsync("projects", "add", "relative", "projects/relative");

        code.ShouldBe(ExitCodes.Usage);
        _host.Output.ToString().ShouldContain("absolute");
    }

    [Fact]
    public async Task Add_DuplicateAlias_IsConfig()
    {
        Seed(SeedConfig);

        var code = await _host.RunAsync("projects", "add", "grimoire", "/home/u/other");

        code.ShouldBe(ExitCodes.Config);
        _host.Output.ToString().ShouldContain("already registered");
    }

    [Fact]
    public async Task Add_DuplicatePath_IsConfig()
    {
        Seed(SeedConfig);

        var code = await _host.RunAsync("projects", "add", "fresh", "/home/u/squeezr");

        code.ShouldBe(ExitCodes.Config);
    }

    [Fact]
    public async Task Add_OnMissingConfig_CreatesConfigFile()
    {
        var code = await _host.RunAsync("projects", "add", "fresh", "/home/u/fresh");

        code.ShouldBe(ExitCodes.Ok);
        File.Exists(_host.ConfigPath).ShouldBeTrue();
        LoadRegistry().Single(record => record.Alias == "fresh").Paths.ShouldBe(["/home/u/fresh"]);
    }

    [Fact]
    public async Task Update_ReplacesPathsAndReportsDelta()
    {
        Seed(SeedConfig);

        var code = await _host.RunAsync("projects", "update", "squeezr", "/home/u/squeezr-new");

        code.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain("paths: /home/u/squeezr, /home/u/squeezr-wt -> /home/u/squeezr-new");
        var registry = LoadRegistry();
        registry.Single(record => record.Alias == "squeezr").Paths.ShouldBe(["/home/u/squeezr-new"]);
        registry.Single(record => record.Alias == "grimoire").Paths.ShouldBe(["/home/u/grimoire"]);
    }

    [Fact]
    public async Task Update_SetsDisplayName()
    {
        Seed(SeedConfig);

        var code = await _host.RunAsync("projects", "update", "squeezr", "--display-name", "Squeezr Index");

        code.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain("display name set to 'Squeezr Index'");
        LoadRegistry().Single(record => record.Alias == "squeezr").DisplayName.ShouldBe("Squeezr Index");
    }

    [Fact]
    public async Task Update_ClearsDisplayNameWithEmptyValue()
    {
        Seed(SeedConfig);

        var code = await _host.RunAsync("projects", "update", "grimoire", "--display-name", "");

        code.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain("display name 'Grimoire' cleared");
        LoadRegistry().Single(record => record.Alias == "grimoire").DisplayName.ShouldBeNull();
    }

    [Fact]
    public async Task Update_SetsAndClearsBoardProjectId()
    {
        Seed(SeedConfig);

        var setCode = await _host.RunAsync("projects", "update", "squeezr", "--board-project-id", "board-9");
        var reloadAfterSet = LoadRegistry().Single(record => record.Alias == "squeezr").BoardProjectId;
        var clearCode = await _host.RunAsync("projects", "update", "squeezr", "--board-project-id", "");
        var reloadAfterClear = LoadRegistry().Single(record => record.Alias == "squeezr").BoardProjectId;

        setCode.ShouldBe(ExitCodes.Ok);
        reloadAfterSet.ShouldBe("board-9");
        _host.Output.ToString().ShouldContain("board project set to 'board-9'");
        clearCode.ShouldBe(ExitCodes.Ok);
        reloadAfterClear.ShouldBeNull();
    }

    [Fact]
    public async Task Update_WithSameValues_ReportsNoChanges()
    {
        Seed(SeedConfig);

        var code = await _host.RunAsync("projects", "update", "grimoire", "--display-name", "Grimoire");

        code.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain("Updated project 'grimoire': no changes");
    }

    [Fact]
    public async Task Update_UnknownAlias_IsNotFound()
    {
        Seed(SeedConfig);

        var code = await _host.RunAsync("projects", "update", "ghost", "/home/u/ghost");

        code.ShouldBe(ExitCodes.NotFound);
    }

    [Fact]
    public async Task Update_WithoutChanges_IsUsage()
    {
        Seed(SeedConfig);

        var code = await _host.RunAsync("projects", "update", "squeezr");

        code.ShouldBe(ExitCodes.Usage);
    }

    [Fact]
    public async Task Remove_RemovesOnlyTargetRecord()
    {
        Seed(SeedConfig);

        var code = await _host.RunAsync("projects", "remove", "squeezr");

        code.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain("Removed project 'squeezr'");
        var registry = LoadRegistry();
        registry.Count.ShouldBe(1);
        registry.Single().Alias.ShouldBe("grimoire");
        registry.Single().DisplayName.ShouldBe("Grimoire");
    }

    [Fact]
    public async Task Remove_LastRecord_LeavesEmptyRegistry()
    {
        Seed("""{ "projects": [ { "alias": "solo", "paths": ["/home/u/solo"] } ] }""");

        var code = await _host.RunAsync("projects", "remove", "solo");

        code.ShouldBe(ExitCodes.Ok);
        LoadRegistry().ShouldBeEmpty();

        var listCode = await _host.RunAsync("projects", "list");
        listCode.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain("No projects registered.");
    }

    [Fact]
    public async Task Remove_UnknownAlias_IsNotFound()
    {
        Seed(SeedConfig);

        var code = await _host.RunAsync("projects", "remove", "ghost");

        code.ShouldBe(ExitCodes.NotFound);
    }

    [Fact]
    public async Task UnknownAction_IsUsage()
    {
        var code = await _host.RunAsync("projects", "export");

        code.ShouldBe(ExitCodes.Usage);
        _host.Output.ToString().ShouldContain("expected: list, add, update, remove");
    }

    private IReadOnlyList<ProjectAliasRecord> LoadRegistry() => new ConfigLoader(_host.ConfigPath).Load().Projects;

    private void Seed(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_host.ConfigPath)!);
        File.WriteAllText(_host.ConfigPath, json);
    }

    public void Dispose() => _host.Dispose();
}
