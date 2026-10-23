using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

public sealed class ProjectsToolTests
{
    private static readonly FakeProjectRegistryStore Seed = new(
    [
        new ProjectAliasRecord { Alias = "grimoire", Paths = ["/src/grimoire"] },
        new ProjectAliasRecord { Alias = "alpha", Paths = ["/src/alpha", "/src/alpha-wt"], BoardProjectId = "b-1", DisplayName = "Alpha" },
    ]);

    private static async Task<(McpTestSession Session, FakeProjectRegistryStore Registry)> StartAsync(
        FakeBoardProjectSource? board = null)
    {
        var registry = new FakeProjectRegistryStore(Seed.Records);
        var session = await McpTestSession.StartAsync(registryStore: registry, board: board);
        return (session, registry);
    }

    [Fact]
    public async Task List_ReturnsCompactRecordsAndCount()
    {
        var (session, _) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "List",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("count").GetInt32().ShouldBe(2);
        var projects = payload.GetProperty("projects").EnumerateArray().ToArray();
        var grimoire = projects.Single(entry => entry.GetProperty("alias").GetString() == "grimoire");
        grimoire.GetProperty("paths").EnumerateArray().Single().GetString().ShouldBe("/src/grimoire");
        grimoire.TryGetProperty("boardProjectId", out _).ShouldBeFalse();
        grimoire.TryGetProperty("displayName", out _).ShouldBeFalse();
        var alpha = projects.Single(entry => entry.GetProperty("alias").GetString() == "alpha");
        alpha.GetProperty("boardProjectId").GetString().ShouldBe("b-1");
        alpha.GetProperty("displayName").GetString().ShouldBe("Alpha");
    }

    [Fact]
    public async Task List_EmptyRegistry_ReturnsZeroCount()
    {
        var session = await McpTestSession.StartAsync(registryStore: new FakeProjectRegistryStore());

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "List",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("count").GetInt32().ShouldBe(0);
        result.Payload().GetProperty("projects").EnumerateArray().ShouldBeEmpty();
    }

    [Fact]
    public async Task List_WithMutationArguments_IsParamConflict()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "List",
            ["alias"] = "grimoire",
            ["paths"] = new[] { "/src/elsewhere" },
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.ParamConflict);
        param.ShouldBe("alias");
        message.ShouldContain("drop alias, paths");
        registry.Mutations.ShouldBe(0);
    }

    [Fact]
    public async Task Add_CreatesRecordWithOptionalFields()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Add",
            ["alias"] = "fresh",
            ["paths"] = new[] { "/src/fresh", "/src/fresh-wt" },
            ["boardProjectId"] = "  b-9 ",
            ["displayName"] = "Fresh Project",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("action").GetString().ShouldBe("Add");
        var project = payload.GetProperty("project");
        project.GetProperty("alias").GetString().ShouldBe("fresh");
        project.GetProperty("paths").EnumerateArray().Select(path => path.GetString())
            .ShouldBe(["/src/fresh", "/src/fresh-wt"]);
        project.GetProperty("boardProjectId").GetString().ShouldBe("b-9");
        project.GetProperty("displayName").GetString().ShouldBe("Fresh Project");
        var fresh = registry.Records.Single(record => record.Alias == "fresh");
        fresh.Paths.ShouldBe(["/src/fresh", "/src/fresh-wt"]);
        fresh.BoardProjectId.ShouldBe("b-9");
        fresh.DisplayName.ShouldBe("Fresh Project");
    }

    [Fact]
    public async Task Add_MissingAlias_IsInvalidRequest()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Add",
            ["paths"] = new[] { "/src/fresh" },
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("alias");
        message.ShouldContain("required");
        message.ShouldContain("Nothing was applied by this call.");
        registry.Records.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Add_InvalidAlias_TeachesKebabCase()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Add",
            ["alias"] = "Slurpy.AI",
            ["paths"] = new[] { "/src/slurpy" },
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("alias");
        message.ShouldContain("'Slurpy.AI'");
        message.ShouldContain("kebab-case");
        registry.Records.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Add_MissingPaths_IsInvalidRequest()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Add",
            ["alias"] = "fresh",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("paths");
        message.ShouldContain("absolute");
        registry.Records.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Add_RelativePath_IsInvalidRequest()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Add",
            ["alias"] = "fresh",
            ["paths"] = new[] { "projects/relative" },
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("paths");
        registry.Records.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Add_DuplicateAlias_IsInvalidRequestWithNothingApplied()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Add",
            ["alias"] = "grimoire",
            ["paths"] = new[] { "/src/other" },
        });

        var (code, message, _) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        message.ShouldContain("'grimoire' is already registered");
        message.ShouldContain("Nothing was applied by this call.");
        registry.Mutations.ShouldBe(0);
    }

    [Fact]
    public async Task Add_DuplicatePath_IsInvalidRequestFromTheRegistryValidator()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Add",
            ["alias"] = "fresh",
            ["paths"] = new[] { "/src/grimoire" },
        });

        var (code, message, _) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        message.ShouldContain("already registered");
        registry.Records.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Add_DuplicateBoardId_IsInvalidRequestFromTheRegistryValidator()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Add",
            ["alias"] = "fresh",
            ["paths"] = new[] { "/src/fresh" },
            ["boardProjectId"] = "b-1",
        });

        var (code, message, _) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        message.ShouldContain("boardProjectId 'b-1' is already registered");
        registry.Records.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Update_ReplacesTheWholePathList()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Update",
            ["alias"] = "alpha",
            ["paths"] = new[] { "/src/alpha-moved" },
        });

        result.ShouldSucceed();
        var project = result.Payload().GetProperty("project");
        project.GetProperty("paths").EnumerateArray().Select(path => path.GetString())
            .ShouldBe(["/src/alpha-moved"]);
        registry.Records.Single(record => record.Alias == "alpha").Paths.ShouldBe(["/src/alpha-moved"]);
        registry.Records.Single(record => record.Alias == "alpha").DisplayName.ShouldBe("Alpha");
    }

    [Fact]
    public async Task Update_SetsAndClearsOptionalFields()
    {
        var (session, registry) = await StartAsync();

        var setResult = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Update",
            ["alias"] = "grimoire",
            ["displayName"] = "Grimoire",
        });
        var clearResult = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Update",
            ["alias"] = "grimoire",
            ["displayName"] = "",
        });

        setResult.ShouldSucceed();
        setResult.Payload().GetProperty("project").GetProperty("displayName").GetString().ShouldBe("Grimoire");
        clearResult.ShouldSucceed();
        clearResult.Payload().GetProperty("project").TryGetProperty("displayName", out _).ShouldBeFalse();
        registry.Records.Single(record => record.Alias == "grimoire").DisplayName.ShouldBeNull();
    }

    [Fact]
    public async Task Update_UnknownAlias_IsInvalidRequest()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Update",
            ["alias"] = "ghost",
            ["paths"] = new[] { "/src/ghost" },
        });

        var (code, message, _) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        message.ShouldContain("No registered project 'ghost'");
        message.ShouldContain("Nothing was applied by this call.");
        registry.Records.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Update_WithoutAnythingToChange_IsInvalidRequest()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Update",
            ["alias"] = "grimoire",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("paths");
        message.ShouldContain("Nothing to update");
        registry.Mutations.ShouldBe(0);
    }

    [Fact]
    public async Task Update_EmptyPathsArray_IsInvalidRequest()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Update",
            ["alias"] = "grimoire",
            ["paths"] = Array.Empty<string>(),
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("paths");
        message.ShouldContain("at least one absolute path");
        registry.Mutations.ShouldBe(0);
    }

    [Fact]
    public async Task Remove_DeletesOnlyTheTargetRecord()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Remove",
            ["alias"] = "alpha",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("action").GetString().ShouldBe("Remove");
        payload.GetProperty("project").GetProperty("alias").GetString().ShouldBe("alpha");
        registry.Records.Select(record => record.Alias).ShouldBe(["grimoire"]);
    }

    [Fact]
    public async Task Remove_UnknownAlias_IsInvalidRequest()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Remove",
            ["alias"] = "ghost",
        });

        var (code, message, _) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        message.ShouldContain("No registered project 'ghost'");
        registry.Records.Count.ShouldBe(2);
    }

    [Fact]
    public async Task MissingAction_IsRefusedByTheTransportWithoutReachingTheRegistry()
    {
        var session = await McpTestSession.StartAsync(registryStore: new FakeProjectRegistryStore());

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>());

        (result.IsError ?? false).ShouldBeTrue("a call without the required action must not reach the tool");
        session.Registry.Mutations.ShouldBe(0);
    }

    [Fact]
    public async Task Sync_AddsNewBoardProjectsWithALocalAlias()
    {
        var source = new FakeBoardProjectSource(
        [
            new BoardProject("b-9", "Slurp.AI", "index it", "/src/slurpy"),
        ]);
        var (session, registry) = await StartAsync(source);

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Sync",
        });

        result.ShouldSucceed();
        var added = result.Payload().GetProperty("added").EnumerateArray().ShouldHaveSingleItem();
        added.GetProperty("subject").GetString().ShouldBe("slurp-ai");
        added.GetProperty("detail").GetString().ShouldContain("/src/slurpy");
        var record = registry.Records.Single(entry => entry.BoardProjectId == "b-9");
        record.Alias.ShouldBe("slurp-ai");
        record.Paths.ShouldBe(["/src/slurpy"]);
        record.Description.ShouldBe("index it");
        record.BoardGone.ShouldBeFalse();
    }

    [Fact]
    public async Task Sync_MarksVanishedBoardProjectsAndKeepsTheAlias()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Sync",
        });

        result.ShouldSucceed();
        var gone = result.Payload().GetProperty("gone").EnumerateArray().ShouldHaveSingleItem();
        gone.GetProperty("subject").GetString().ShouldBe("alpha");
        var alpha = registry.Records.Single(entry => entry.Alias == "alpha");
        alpha.BoardGone.ShouldBeTrue();
        alpha.BoardProjectId.ShouldBe("b-1");
        alpha.Paths.ShouldBe(["/src/alpha", "/src/alpha-wt"]);
    }

    [Fact]
    public async Task Sync_WithMutationArguments_IsParamConflict()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Sync",
            ["alias"] = "grimoire",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.ParamConflict);
        param.ShouldBe("alias");
        message.ShouldContain("action Sync takes no other arguments");
        registry.Mutations.ShouldBe(0);
    }

    [Fact]
    public async Task Sync_BoardUnavailable_MapsToUpstreamUnavailableWithoutRegistryTouch()
    {
        var source = new FakeBoardProjectSource { Failure = new UpstreamUnavailableException("overmind") };
        var (session, registry) = await StartAsync(source);

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "Sync",
        });

        var (code, message, _) = result.Error();
        code.ShouldBe(ToolErrorCodes.UpstreamUnavailable);
        message.ShouldContain("overmind");
        message.ShouldContain("Nothing was written by this call");
        registry.Mutations.ShouldBe(0);
    }

    [Fact]
    public async Task Sync_MakesANewAliasResolvableWithoutARestart()
    {
        var source = new FakeBoardProjectSource(
        [
            new BoardProject("b-9", "Slurp.AI", null, "/src/slurpy"),
        ]);
        var (session, _) = await StartAsync(source);
        await session.Client.CallToolAsync("projects", new Dictionary<string, object?> { ["action"] = "Sync" });
        session.Embeddings.Register("freshly synced note", [1f, 0f]);

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "freshly synced note",
            ["scope"] = "Project",
            ["projectId"] = "slurp-ai",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("projectId").GetString().ShouldBe("slurp-ai");
    }

    [Fact]
    public async Task WrongCaseAction_RejectedBeforeAnyRegistryTouch()
    {
        var (session, registry) = await StartAsync();

        var result = await session.Client.CallToolAsync("projects", new Dictionary<string, object?>
        {
            ["action"] = "add",
            ["alias"] = "fresh",
            ["paths"] = new[] { "/src/fresh" },
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("action");
        message.ShouldContain("'add'");
        message.ShouldContain("case-sensitive");
        foreach (var token in new[] { "List", "Add", "Update", "Remove", "Sync" })
        {
            message.ShouldContain(token, Case.Sensitive);
        }

        registry.Records.Count.ShouldBe(2);
    }
}
