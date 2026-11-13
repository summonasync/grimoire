using Grimoire.Cli.Tests.Fakes;
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

    [Fact]
    public async Task Brief_TomeTable_RendersManifestsWithCreatedBy()
    {
        _host.Manifests.Seed(Manifest("alpha", "alpha squad routing"));
        _host.Manifests.Seed(Manifest("beta", "beta squad routing"));

        var code = await _host.RunAsync("brief");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("tomes:");
        output.ShouldContain("alpha by human");
        output.ShouldContain("alpha squad routing");
        output.ShouldContain("beta by human");
        output.ShouldContain("beta squad routing");
        output.ShouldNotContain("(+");
    }

    [Fact]
    public async Task Brief_TomeTable_CapsAtSixteenWithMoreOverflow()
    {
        for (var index = 0; index < 18; index++)
        {
            _host.Manifests.Seed(Manifest($"tome-{index:D2}", "overflow probe"));
        }

        var code = await _host.RunAsync("brief");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("tome-00 by human");
        output.ShouldContain("tome-15 by human");
        output.ShouldNotContain("tome-16 by human");
        output.ShouldNotContain("tome-17 by human");
        output.ShouldContain("(+2 more)");
    }

    [Fact]
    public async Task Brief_WhenTheManifestStoreThrows_RendersTheNoteAndStillSucceeds()
    {
        _host.Manifests.Failure = new InvalidOperationException("qdrant is down");

        var code = await _host.RunAsync("brief");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("unavailable");
        output.ShouldContain("qdrant is down");
        output.ShouldNotContain("by human");
    }

    [Fact]
    public async Task Brief_WhenTheManifestStoreIsDegraded_RendersTheNote()
    {
        _host.Manifests.DegradationReason = "Tome manifest store is unreachable - manifest data may be stale";

        var code = await _host.RunAsync("brief");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("unreachable");
    }

    [Fact]
    public async Task Brief_DailyFlag_RendersTheSameGroupsTheMcpFaceReturns()
    {
        // Mirrors BriefToolTests.Brief_DailyFlag_ReturnsGroupsByActorWithEntriesOldestFirst:
        // same clock instant (FakeClock.DefaultStart == the MCP fixture's BriefClockInstant),
        // same three notes, same actors. The two doors are pinned by the same fixture script;
        // reseeding one side must reseed the other.
        SeedDaily(_host.Store, "medivh opened the board", 0, "medivh", "claude");
        SeedDaily(_host.Store, "medivh pushed the seam", 30, "medivh", "qwen");
        SeedDaily(_host.Store, "thrall wired the door", 10, "thrall", "claude");

        var code = await _host.RunAsync("brief", "--daily");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        var day = ExpectedLocalDay();
        output.ShouldContain("daily:");
        LineIndex(output, $"  {day} today medivh (2)").ShouldNotBe(-1);
        LineIndex(output, $"  {day} today thrall (1)").ShouldNotBe(-1);
        LineIndex(output, " today medivh (2)").ShouldBeLessThan(LineIndex(output, " today thrall (1)"));
        LineIndex(output, "medivh opened the board")
            .ShouldBeLessThan(LineIndex(output, "medivh pushed the seam"));
        LineIndex(output, "  (+").ShouldBe(-1);
        WarningLines(output).ShouldBeEmpty();
    }

    [Fact]
    public async Task Brief_DailyFlag_LastWorkingDayGroupPrintsAheadOfToday()
    {
        SeedDaily(_host.Store, "friday seam landed", -24 * 60, "medivh", "claude");
        SeedDaily(_host.Store, "saturday note on the door", 0, "radim", "claude");

        var code = await _host.RunAsync("brief", "--daily");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        var today = ExpectedLocalDay();
        var lastWorkingDay = ExpectedLocalDay(-24 * 60);
        lastWorkingDay.ShouldNotBe(today);
        LineIndex(output, $"  {lastWorkingDay} last-working-day medivh (1)").ShouldNotBe(-1);
        LineIndex(output, $"  {today} today radim (1)").ShouldNotBe(-1);
        LineIndex(output, " last-working-day medivh").ShouldBeLessThan(LineIndex(output, " today radim"));
        WarningLines(output).ShouldBeEmpty();
    }

    [Fact]
    public async Task Brief_WithoutDailyFlag_RendersNoDailySection()
    {
        var note = SeedDaily(_host.Store, "a daily note plain brief must not announce", 0, "medivh", "claude");

        var plain = await _host.RunAsync("brief");

        plain.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldNotContain("daily:");
        _host.Output.ToString().ShouldContain(note.Id.ToString());

        var declined = await _host.RunAsync("brief", "--daily=false");

        declined.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldNotContain("daily:");
    }

    [Fact]
    public async Task Brief_WithoutDailyFlag_StaysSilentOnWarningsEvenWhenTheZoneIsDead()
    {
        using var host = new TestHost(options: new MemoryEngineOptions { DailyTimeZone = "Not/AZone" });
        var note = SeedDaily(host.Store, "a note the journal path can still show", 0, "medivh", "claude");

        var code = await host.RunAsync("brief");

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        var output = host.Output.ToString();
        output.ShouldNotContain("daily:");
        WarningLines(output).ShouldBeEmpty();
        output.ShouldContain(note.Id.ToString(),
            customMessage: "the note is renderable on this seam, so the silence below is earned and not a cold store");
    }

    [Fact]
    public async Task Brief_DailyFlag_AppearsInHelp()
    {
        var code = await _host.RunAsync("help", "brief");

        code.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain("--daily");
    }

    [Fact]
    public async Task Brief_DailyFlag_NoNotesYet_RendersEmptyState()
    {
        var code = await _host.RunAsync("brief", "--daily");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("daily: (empty)");
        LineIndex(output, "  (+").ShouldBe(-1);
        WarningLines(output).ShouldBeEmpty();
    }

    [Fact]
    public async Task Brief_DailyFlag_UnresolvableZone_PrintsTheZoneWarningAlone()
    {
        using var host = new TestHost(options: new MemoryEngineOptions { DailyTimeZone = "Not/AZone" });

        var code = await host.RunAsync("brief", "--daily");

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        WarningLines(host.Output.ToString()).ShouldBe(
            ["Warning: DailyTimeZone 'Not/AZone' did not resolve; daily buckets use UTC."]);
    }

    [Fact]
    public async Task Brief_DailyFlag_EmptyTag_PrintsTheDisabledSentenceVerbatim()
    {
        using var host = new TestHost(options: new MemoryEngineOptions { DailyTag = "" });

        var code = await host.RunAsync("brief", "--daily");

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        var warnings = WarningLines(host.Output.ToString());
        warnings.ShouldHaveSingleItem();
        warnings[0].ShouldBe("Warning: DailyTag is empty; the daily section is disabled. Set DailyTag to enable it.");
        warnings[0].ShouldNotContain("DailyTimeZone");
    }

    [Fact]
    public async Task Brief_DailyFlag_BothDegraded_PrintsTwoLinesTagFirst()
    {
        using var host = new TestHost(options: new MemoryEngineOptions
        {
            DailyTag = "",
            DailyTimeZone = "Not/AZone",
        });

        var code = await host.RunAsync("brief", "--daily");

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        WarningLines(host.Output.ToString()).ShouldBe(
        [
            "Warning: DailyTag is empty; the daily section is disabled. Set DailyTag to enable it.",
            "Warning: DailyTimeZone 'Not/AZone' did not resolve; daily buckets use UTC.",
        ]);
    }

    [Fact]
    public async Task Brief_DailyFlag_ResolvableZoneWithConfiguredTag_PrintsNoWarnings()
    {
        using var host = new TestHost(options: new MemoryEngineOptions { DailyTimeZone = "Europe/Moscow" });
        SeedDaily(host.Store, "moscow note under a healthy config", 0, "medivh", "claude");

        var code = await host.RunAsync("brief", "--daily");

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        WarningLines(host.Output.ToString()).ShouldBeEmpty();
        host.Output.ToString().ShouldContain("daily:");
    }

    [Fact]
    public async Task Brief_DailyFlag_OverBudgetTail_PrintsKeptEntriesAndMoreLine()
    {
        using var host = new TestHost(options: new MemoryEngineOptions { BriefDailyMaxTotalChars = 100 });
        var kept = SeedDaily(host.Store, new string('a', 40), 0, "medivh", "claude");
        var alsoKept = SeedDaily(host.Store, new string('b', 40), 10, "medivh", "claude");
        var swallowed = SeedDaily(host.Store, new string('c', 40), 20, "medivh", "claude");

        var code = await host.RunAsync("brief", "--daily");

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        var output = host.Output.ToString();
        output.ShouldContain($"  {ExpectedLocalDay()} today medivh (2)");
        output.ShouldContain(kept.Id.ToString());
        output.ShouldContain(alsoKept.Id.ToString());
        output.ShouldNotContain(swallowed.Id.ToString());
        output.ShouldContain("  (+1 more)");
    }

    [Fact]
    public async Task Brief_DailyFlag_OversizedEntry_PrintsCapAndMarker()
    {
        SeedDaily(_host.Store, new string('x', 2000), 0, "medivh", "claude");

        var code = await _host.RunAsync("brief", "--daily");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain(new string('x', 1500));
        output.ShouldNotContain(new string('x', 1501));
        LineIndex(output, "      (truncated)").ShouldNotBe(-1);
    }

    private static MemoryRecord SeedDaily(
        RecordingMemoryStore store,
        string content,
        int minutesFromClockStart,
        string alias,
        string vendor)
    {
        var created = FakeClock.DefaultStart.AddMinutes(minutesFromClockStart);
        var record = new MemoryRecord
        {
            Id = Guid.NewGuid(),
            Content = content,
            Scope = MemoryScope.Project,
            Kind = MemoryKind.Journal,
            Actor = MemoryActor.Agent(alias, vendor),
            Tags = ["daily-notes"],
            CreatedAt = created,
            LastAccessedAt = created,
        };
        store.Seed(record, [1f, 0f]);
        return record;
    }

    private static string ExpectedLocalDay(int minutesFromClockStart = 0) =>
        TimeZoneInfo.ConvertTime(FakeClock.DefaultStart.AddMinutes(minutesFromClockStart), TimeZoneInfo.Local)
            .ToString("yyyy-MM-dd");

    private static int LineIndex(string output, string needle) =>
        Array.FindIndex(
            output.Split('\n'),
            line => line.TrimEnd('\r').Contains(needle, StringComparison.Ordinal));

    private static string[] WarningLines(string output) =>
        output.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith("Warning: ", StringComparison.Ordinal))
            .ToArray();

    private static TomeManifest Manifest(string key, string description) => new()
    {
        Key = key,
        Description = description,
        CreatedBy = "human",
        CreatedAt = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero),
    };

    private void SeedPersona(string content, string sectionTag) =>
        _host.Store.Add(
            content,
            [1f, 0f],
            scope: MemoryScope.Personal,
            kind: MemoryKind.Fact,
            tags: ["persona", sectionTag]);

    public void Dispose() => _host.Dispose();
}
