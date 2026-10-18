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
        _host.Store.Add("tagged", [1f, 0f], tags: ["a"]);

        var code = await _host.RunAsync("list", "--scope", "project,global", "--tag", "a,b", "--limit", "3");

        code.ShouldBe(ExitCodes.Ok);
        var filter = _host.Store.ListedFilters.ShouldHaveSingleItem();
        filter.Scopes.ShouldBe([MemoryScope.Project, MemoryScope.Global]);
        filter.Tags.ShouldBe(["a", "b"]);
        filter.Limit.ShouldBe(3);
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

    public void Dispose() => _host.Dispose();
}
