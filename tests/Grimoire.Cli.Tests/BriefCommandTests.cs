using System.Text.Json;
using System.Text.RegularExpressions;
using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class BriefCommandTests : IDisposable
{
    private readonly TestHost _host = new();

    private static readonly Regex FactRow = new(@"^  \d\.\d+ [0-9a-f]{8}-", RegexOptions.IgnoreCase);

    [Fact]
    public async Task Brief_NoOmissions_TextOutputIsThePreFlagGoldenByteForByte()
    {
        SeedPayload(_host.Store);

        var code = await _host.RunAsync("brief");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldNotContain("omitted");
        output.ReplaceLineEndings("\n").ShouldBe(NoOmissionGolden + "\n");
    }

    [Fact]
    public async Task Brief_FormatText_IsTheDefaultRender()
    {
        using var plain = new TestHost();
        using var declared = new TestHost();
        SeedPayload(plain.Store);
        SeedPayload(declared.Store);

        (await plain.RunAsync("brief")).ShouldBe(ExitCodes.Ok, plain.Output.ToString());
        (await declared.RunAsync("brief", "--format", "text")).ShouldBe(ExitCodes.Ok, declared.Output.ToString());

        declared.Output.ToString().ShouldBe(plain.Output.ToString());
    }

    [Fact]
    public async Task Brief_FormatHook_PrintsTheSessionStartEnvelope()
    {
        SeedPayload(_host.Store);

        var code = await _host.RunAsync("brief", "--format", "hook");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var stdout = _host.Output.ToString();
        stdout.ShouldNotContain("not wired");
        using var document = JsonDocument.Parse(stdout);
        var output = document.RootElement.GetProperty("hookSpecificOutput");
        output.GetProperty("hookEventName").GetString().ShouldBe("SessionStart");
        var context = output.GetProperty("additionalContext").GetString();
        context.ShouldNotBeNullOrWhiteSpace();
        context!.ShouldContain("deploys happen on friday");
        context.ShouldContain("superseded by the rewrite");
        context.ShouldContain("user asked for the brief");
        context.ShouldContain("tomes: (empty)");
    }

    [Fact]
    public async Task Brief_FormatFlag_RejectsValuesOutsideTheEnum()
    {
        var code = await _host.RunAsync("brief", "--format", "galaxy");

        code.ShouldBe(ExitCodes.Usage, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("Unknown format 'galaxy' (expected: text, hook).");
    }

    [Fact]
    public async Task Brief_NewFlags_AreListedInHelp()
    {
        var code = await _host.RunAsync("help", "brief");

        code.ShouldBe(ExitCodes.Ok);
        var output = _host.Output.ToString();
        output.ShouldContain("--format <text|hook>");
        output.ShouldContain("--max-chars <n>");
        output.ShouldContain("--since <time>");
        output.ShouldContain("--until <time>");
    }

    [Fact]
    public async Task Brief_WindowFlags_MapToTheFilterBoundsOfEveryDatedRead()
    {
        SeedPayload(_host.Store);

        var code = await _host.RunAsync(
            "brief",
            "--since", "2026-10-10T00:00:00+00:00",
            "--until", "2026-10-15T00:00:00+00:00");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var dated = _host.Store.ListedFilters
            .Where(filter => filter.CreatedAfter is not null || filter.CreatedBefore is not null)
            .ToArray();
        dated.Length.ShouldBe(2);
        foreach (var filter in dated)
        {
            filter.CreatedAfter.ShouldBe(new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero));
            filter.CreatedBefore.ShouldBe(new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero));
        }
    }

    [Fact]
    public async Task Brief_DateOnlyWindow_IsResolvedInTheLocalZoneNotAsAnUtcLiteral()
    {
        SeedPayload(_host.Store);
        var midnight = new DateTime(2026, 10, 10);
        var expected = new DateTimeOffset(midnight, TimeZoneInfo.Local.GetUtcOffset(midnight)).ToUniversalTime();

        var code = await _host.RunAsync("brief", "--since", "2026-10-10");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var dated = _host.Store.ListedFilters.Where(filter => filter.CreatedAfter is not null).ToArray();
        dated.Length.ShouldBe(2);
        foreach (var filter in dated)
        {
            filter.CreatedAfter.ShouldBe(expected);
        }
    }

    [Fact]
    public async Task Brief_UntilFlag_ExcludesTheRecordStandingExactlyOnTheBound()
    {
        var bound = LocalMidnight(new DateTime(2026, 10, 12));
        SeedRecord(_host.Store, Guid.Parse("66666666-6666-6666-6666-666666666666"), "journal on the bound",
            MemoryScope.Project, MemoryKind.Journal, bound);
        SeedRecord(_host.Store, Guid.Parse("77777777-7777-7777-7777-777777777777"), "journal an hour before",
            MemoryScope.Project, MemoryKind.Journal, bound.AddHours(-1));
        SeedRecord(_host.Store, Guid.Parse("88888888-8888-8888-8888-888888888888"), "fact on the bound",
            MemoryScope.Project, MemoryKind.Fact, bound);

        // The Cli fake ignores CreatedBefore; this green is load-bearing on Core's Matches
        // re-check — delete that branch and this test reddens (review 046-05 mutation 3).
        var code = await _host.RunAsync("brief", "--until", "2026-10-12");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("journal an hour before");
        output.ShouldNotContain("journal on the bound");
        output.ShouldNotContain("88888888-8888-8888-8888-888888888888");
        LineIndex(output, $"    {Guid.Parse("66666666-6666-6666-6666-666666666666")}").ShouldBe(-1);
    }

    [Fact]
    public async Task Brief_SinceFlag_KeepsTheRecordStandingExactlyOnTheBound()
    {
        var bound = LocalMidnight(new DateTime(2026, 10, 10));
        SeedRecord(_host.Store, Guid.Parse("99999999-9999-9999-9999-999999999999"), "note on the lower bound",
            MemoryScope.Project, MemoryKind.Journal, bound);

        var code = await _host.RunAsync("brief", "--since", "2026-10-10");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("note on the lower bound");
    }

    [Fact]
    public async Task Brief_InvertedWindow_ExitsUsageInsteadOfReadingEmpty()
    {
        SeedPayload(_host.Store);

        var code = await _host.RunAsync("brief", "--since", "2026-10-15", "--until", "2026-10-10");

        code.ShouldBe(ExitCodes.Usage, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("'since' must not be later than 'until'.");
    }

    [Fact]
    public async Task Brief_UnparsableWindow_ExitsUsageNamingTheFlagAndTheValue()
    {
        var code = await _host.RunAsync("brief", "--until", "last tuesday");

        code.ShouldBe(ExitCodes.Usage, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("'until' must be an ISO-8601 timestamp or a yyyy-MM-dd date but was 'last tuesday'.");
    }

    [Fact]
    public async Task Brief_MaxCharsBelowTheSectionBudget_OmitsSectionsAndNamesTheRecords()
    {
        SeedPayload(_host.Store);

        var plain = await _host.RunAsync("brief");
        plain.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("deploys happen on friday");
        _host.Output.ToString().ShouldNotContain("omitted");

        using var tight = new TestHost();
        SeedPayload(tight.Store);

        var cut = await tight.RunAsync("brief", "--max-chars", "5");

        cut.ShouldBe(ExitCodes.Ok, tight.Output.ToString());
        var output = tight.Output.ToString();
        output.ShouldContain("persona: (omitted: over budget)");
        NextHeading(output, "persona").ShouldBe("facts: (omitted: over budget)");
        LineIndex(output, "    22222222-2222-2222-2222-222222222222 over-budget: deploys happen on friday")
            .ShouldNotBe(-1);
        LineIndex(output, "    33333333-3333-3333-3333-333333333333 over-budget: the store is qdrant")
            .ShouldNotBe(-1);
        output.ShouldContain("  omitted 2:");
    }

    [Fact]
    public async Task Brief_MaxCharsRejectsZeroAndGarbage()
    {
        (await _host.RunAsync("brief", "--max-chars", "0")).ShouldBe(ExitCodes.Usage, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("--max-chars must be a positive integer, got '0'.");

        var garbage = await _host.RunAsync("brief", "--max-chars", "wide");

        garbage.ShouldBe(ExitCodes.Usage, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("--max-chars must be a positive integer, got 'wide'.");
    }

    [Fact]
    public async Task Brief_MaxCharsWithDaily_BindsTheDailyPurseToTheSameEnvelope()
    {
        var note = SeedDaily(_host.Store, new string('x', 2000), 0, "medivh", "claude");

        var code = await _host.RunAsync("brief", "--daily", "--max-chars", "5");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldNotContain(new string('x', 81),
            customMessage: "the daily purse ignored --max-chars (review 046-05, N-2): the flood is still in the render.");
        LineIndex(output, $"    {note.Id} daily-purse: {new string('x', 80)} [cut]").ShouldNotBe(-1);
    }


    [Fact]
    public async Task Brief_DigestLine_PrintsTheExcerptAsCoreFoldedItAndCutsNothingFurther()
    {
        SeedRecord(_host.Store, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "the note that stayed",
            MemoryScope.Project, MemoryKind.Journal, FakeClock.DefaultStart);
        SeedRecord(_host.Store, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), new string('x', 500),
            MemoryScope.Project, MemoryKind.Journal, FakeClock.DefaultStart.AddHours(-2));

        var code = await _host.RunAsync("brief", "--journal-per-scope", "1");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        LineIndex(output, $"    {Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")} read-limit: {new string('x', 80)} [cut]")
            .ShouldNotBe(-1);
        output.ShouldNotContain(new string('x', 81));
    }

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

        var output = _host.Output.ToString();
        var factRows = output.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => FactRow.IsMatch(line))
            .ToArray();
        factRows.Length.ShouldBe(1);
        var trimmed = new[] { first.Id, second.Id }
            .First(id => !factRows[0].Contains(id.ToString(), StringComparison.Ordinal));
        LineIndex(output, $"    {trimmed} read-limit: fact").ShouldNotBe(-1);
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
        var fact = _host.Store.Add("small fact", [1f, 0f], kind: MemoryKind.Fact);

        var code = await _host.RunAsync("brief");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("facts: (omitted: over budget)");
        LineIndex(output, $"    {fact.Id} over-budget: small fact").ShouldNotBe(-1);
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
        output.ShouldContain("  (+1 more)");
        LineIndex(output, $"    {swallowed.Id} daily-purse: ").ShouldNotBe(-1);
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

    private static void SeedPayload(RecordingMemoryStore store)
    {
        SeedRecord(store, Guid.Parse("11111111-1111-1111-1111-111111111111"), "senior backend dev",
            MemoryScope.Personal, MemoryKind.Fact, FakeClock.DefaultStart, ["persona", "persona:identity"]);
        SeedRecord(store, Guid.Parse("22222222-2222-2222-2222-222222222222"), "deploys happen on friday",
            MemoryScope.Project, MemoryKind.Fact, FakeClock.DefaultStart.AddHours(-60));
        SeedRecord(store, Guid.Parse("33333333-3333-3333-3333-333333333333"), "the store is qdrant",
            MemoryScope.Global, MemoryKind.Fact, FakeClock.DefaultStart.AddHours(-120));
        SeedRecord(store, Guid.Parse("44444444-4444-4444-4444-444444444444"), "user asked for the brief",
            MemoryScope.Project, MemoryKind.Journal, FakeClock.DefaultStart.AddHours(-180));
        SeedRecord(store, Guid.Parse("55555555-5555-5555-5555-555555555555"), "note the rewrite swallowed",
            MemoryScope.Project, MemoryKind.Journal, FakeClock.DefaultStart.AddHours(-240),
            status: MemoryStatus.Tombstoned, tombstoneReason: "superseded by the rewrite");
    }

    private static void SeedRecord(
        RecordingMemoryStore store,
        Guid id,
        string content,
        MemoryScope scope,
        MemoryKind kind,
        DateTimeOffset created,
        IReadOnlyList<string>? tags = null,
        MemoryStatus status = MemoryStatus.Active,
        string? tombstoneReason = null)
    {
        store.Seed(
            new MemoryRecord
            {
                Id = id,
                Content = content,
                Scope = scope,
                Kind = kind,
                Actor = MemoryActor.Human,
                Tags = tags ?? [],
                CreatedAt = created,
                LastAccessedAt = created,
                Status = status,
                TombstoneReason = tombstoneReason,
                TombstonedAt = status == MemoryStatus.Tombstoned ? created : null,
            },
            [1f, 0f]);
    }

    private static DateTimeOffset LocalMidnight(DateTime date) =>
        new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date)).ToUniversalTime();

    private static string NextHeading(string output, string section)
    {
        var lines = output.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        var index = Array.FindIndex(lines, line => line.StartsWith(section, StringComparison.Ordinal));
        index.ShouldBeGreaterThanOrEqualTo(0, $"no {section} heading in:{Environment.NewLine}{output}");
        for (var next = index + 1; next < lines.Length; next++)
        {
            if (lines[next].Length > 0 && !lines[next].StartsWith(' '))
            {
                return lines[next];
            }
        }

        return string.Empty;
    }

    // Captured from the pre-flag tree at d7840d6 before this slice edited the renderer: 542 bytes, the
    // same bytes under TZ unset (Europe/Minsk) and TZ=UTC. This string, not a ShouldContain, is AC3.
    private const string NoOmissionGolden = """
        persona:
          identity:
            senior backend dev
          tone:
            (empty)
          preferences:
            (empty)
          rules:
            (empty)
        facts:
          0.722 33333333-3333-3333-3333-333333333333 Global 2026-10-12 12:00:00Z
            the store is qdrant
          0.708 22222222-2222-2222-2222-222222222222 Project 2026-10-15 00:00:00Z
            deploys happen on friday
        tombstones:
          55555555-5555-5555-5555-555555555555 2026-10-07 12:00:00Z
            superseded by the rewrite
        journal:
          44444444-4444-4444-4444-444444444444 Project 2026-10-10 00:00:00Z
            user asked for the brief
        tomes: (empty)
        """;

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
