using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class ListCommandTests : IDisposable
{
    private readonly TestHost _host = new();

    [Fact]
    public async Task List_DefaultsToExplicitActiveStatus()
    {
        _host.Store.Add("active record", [1f, 0f]);

        var code = await _host.RunAsync("list");

        code.ShouldBe(ExitCodes.Ok);
        var filter = _host.Store.ListedFilters.ShouldHaveSingleItem();
        filter.Status.ShouldBe(MemoryStatus.Active);
    }

    [Fact]
    public async Task List_StatusFlagIsPassedThroughExplicitly()
    {
        _host.Store.Add("buried", [1f, 0f], status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: _host.Clock.UtcNow);

        var code = await _host.RunAsync("list", "--status", "tombstoned");

        code.ShouldBe(ExitCodes.Ok);
        _host.Store.ListedFilters.ShouldHaveSingleItem().Status.ShouldBe(MemoryStatus.Tombstoned);
        _host.Output.ToString().ShouldContain("buried");
    }

    [Fact]
    public async Task List_FlowsScopeTagsAndLimit()
    {
        _host.Store.Add("first", [1f, 0f], tags: ["a"]);
        _host.Store.Add("second", [1f, 0f], tags: ["a"]);
        _host.Store.Add("third", [1f, 0f], tags: ["a"]);
        _host.Store.Add("fourth", [1f, 0f], tags: ["a"]);

        var code = await _host.RunAsync("list", "--scope", "project,global", "--tag", "a,b", "--limit", "3");

        code.ShouldBe(ExitCodes.Ok);
        var filter = _host.Store.ListedFilters.ShouldHaveSingleItem();
        filter.Scopes.ShouldBe([MemoryScope.Project, MemoryScope.Global]);
        filter.Tags.ShouldBe(["a", "b"]);
        filter.Limit.ShouldBeNull();
        var output = _host.Output.ToString();
        output.ShouldContain("first");
        output.ShouldContain("second");
        output.ShouldContain("third");
        output.ShouldNotContain("fourth");
    }

    [Fact]
    public async Task List_EnvTomeLandsTheReadThere()
    {
        using var host = new TestHost(
            variables: new Dictionary<string, string> { ["GRIMOIRE_TOME"] = "platform-work" });
        SeedInTome(host, "note inside the env tome", "platform-work");
        SeedInTome(host, "note outside the env tome", "default");

        var code = await host.RunAsync("list");

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        host.Store.ListedFilters.ShouldHaveSingleItem().Tome.ShouldBe("platform-work");
        var output = host.Output.ToString();
        output.ShouldContain("note inside the env tome");
        output.ShouldNotContain("note outside the env tome");
    }

    [Fact]
    public async Task List_UnknownStatus_ExitsUsage()
    {
        var code = await _host.RunAsync("list", "--status", "archived");

        code.ShouldBe(ExitCodes.Usage);
    }

    [Fact]
    public async Task List_EmptyResult_PrintsNotice()
    {
        var code = await _host.RunAsync("list");

        code.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain("No memories.");
    }

    private static void SeedInTome(TestHost host, string content, string tome)
    {
        host.Store.Seed(
            new MemoryRecord
            {
                Id = Guid.NewGuid(),
                Content = content,
                Scope = MemoryScope.Project,
                Kind = MemoryKind.Journal,
                Actor = MemoryActor.Human,
                Tome = tome,
                CreatedAt = host.Clock.UtcNow,
                LastAccessedAt = host.Clock.UtcNow,
            },
            [1f, 0f]);
    }

    public void Dispose() => _host.Dispose();
}
