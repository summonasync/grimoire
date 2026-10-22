using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class BriefCommandTests : IDisposable
{
    private readonly TestHost _host = new();

    [Fact]
    public async Task Brief_RendersPopulatedSections()
    {
        SeedPersona("senior backend dev", "persona:identity");
        var fact = _host.Store.Add("deploys happen on friday", [1f, 0f], kind: MemoryKind.Fact);
        var entry = _host.Store.Add("user asked for the brief", [1f, 0f]);
        var buried = _host.Store.Add(
            "stale note",
            [1f, 0f],
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "superseded",
            tombstonedAt: _host.Clock.UtcNow);

        var code = await _host.RunAsync("brief");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("persona:");
        output.ShouldContain("identity:");
        output.ShouldContain("senior backend dev");
        output.ShouldContain(fact.Id.ToString());
        output.ShouldContain("deploys happen on friday");
        output.ShouldContain(entry.Id.ToString());
        output.ShouldContain("user asked for the brief");
        output.ShouldContain(buried.Id.ToString());
        output.ShouldContain("superseded");
        output.ShouldNotContain("(omitted");
        output.ShouldNotContain("(truncated)");
    }

    [Fact]
    public async Task Brief_ColdStart_ShowsEmptyMarkers()
    {
        var code = await _host.RunAsync("brief");

        code.ShouldBe(ExitCodes.Ok);
        var output = _host.Output.ToString();
        output.ShouldContain("persona: (empty)");
        output.ShouldContain("facts: (empty)");
        output.ShouldContain("tombstones: (empty)");
        output.ShouldContain("journal: (empty)");
    }

    [Fact]
    public async Task Brief_FlowsProjectScopeAndHints()
    {
        var first = _host.Store.Add("fact one", [1f, 0f], kind: MemoryKind.Fact, projectId: "grimoire");
        var second = _host.Store.Add("fact two", [1f, 0f], kind: MemoryKind.Fact, projectId: "grimoire");

        var code = await _host.RunAsync(
            "brief",
            "--project", "grimoire",
            "--scope", "project",
            "--facts-per-scope", "1",
            "--journal-per-scope", "2",
            "--tombstone-limit", "3");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var briefFilters = _host.Store.ListedFilters.Where(filter => filter.Tags.Count == 0).ToArray();
        briefFilters.Length.ShouldBe(4);

        var backlogRead = briefFilters.Single(filter => filter.ProjectId is null);
        backlogRead.Kind.ShouldBe(MemoryKind.Journal);
        backlogRead.Scopes.ShouldBe(MemoryScopes.LongTerm);

        foreach (var filter in briefFilters.Where(filter => filter.ProjectId is not null))
        {
            filter.ProjectId.ShouldBe("grimoire");
            filter.Scopes.ShouldBe([MemoryScope.Project]);
        }

        var rendered = new[] { first.Id.ToString(), second.Id.ToString() }.Count(_host.Output.ToString().Contains);
        rendered.ShouldBe(1);
    }

    [Fact]
    public async Task Brief_TruncatedJournalEntry_ShowsMarker()
    {
        _host.Store.Add(new string('x', 500), [1f, 0f]);

        var code = await _host.RunAsync("brief");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("(truncated)");
        output.ShouldNotContain(new string('x', 401));
    }

    [Fact]
    public async Task Brief_OverBudgetSection_ShowsOmittedMarker()
    {
        SeedPersona(new string('p', 7995), "persona:identity");
        _host.Store.Add("small fact", [1f, 0f], kind: MemoryKind.Fact);

        var code = await _host.RunAsync("brief");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("facts: (omitted: over budget)");
        output.ShouldNotContain("small fact");
        output.ShouldContain("journal: (empty)");
    }

    [Fact]
    public async Task Brief_UnknownScope_ExitsUsage()
    {
        var code = await _host.RunAsync("brief", "--scope", "galaxy");

        code.ShouldBe(ExitCodes.Usage);
        _host.Output.ToString().ShouldContain("error [usage]");
    }

    [Fact]
    public async Task Brief_HelpTopic_ListsOptions()
    {
        var code = await _host.RunAsync("help", "brief");

        code.ShouldBe(ExitCodes.Ok);
        var output = _host.Output.ToString();
        output.ShouldContain("--project");
        output.ShouldContain("--facts-per-scope");
        output.ShouldContain("--journal-per-scope");
        output.ShouldContain("--tombstone-limit");
    }

    private void SeedPersona(string content, string sectionTag) =>
        _host.Store.Add(
            content,
            [1f, 0f],
            scope: MemoryScope.Personal,
            kind: MemoryKind.Fact,
            tags: ["persona", sectionTag]);

    public void Dispose() => _host.Dispose();
}
