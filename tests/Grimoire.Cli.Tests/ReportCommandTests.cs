using System.Globalization;
using System.Text.Json;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class ReportCommandTests : IDisposable
{
    private readonly TestHost _host = new();

    [Fact]
    public async Task Report_TwoActors_GroupsByActorWithNewestFirstInsideEachGroup()
    {
        var start = DayStart();
        Note("zeta-early", start.AddHours(2), "zeta", "qwen");
        Note("zeta-late", start.AddHours(10), "zeta", "qwen");
        Note("alpha-noon", start.AddHours(6), "alpha", "qwen");

        var output = await Report(DayArgument());

        output.ShouldContain("## alpha+qwen (1 record)");
        output.ShouldContain("## zeta+qwen (2 records)");
        output.IndexOf("## alpha+qwen", StringComparison.Ordinal)
            .ShouldBeLessThan(output.IndexOf("## zeta+qwen", StringComparison.Ordinal));
        output.IndexOf("zeta-late", StringComparison.Ordinal)
            .ShouldBeLessThan(output.IndexOf("zeta-early", StringComparison.Ordinal));
        output.ShouldContain("Records: 3");
        output.ShouldContain("Tags: daily-notes");
        output.ShouldContain($"Daily notes {DayLabel()}");
        output.ShouldContain($"Window: {Window()}");
    }

    [Fact]
    public async Task Report_RecordStraddlingTheLocalDayBoundaryLandsInExactlyOneReport()
    {
        Note("before-midnight", DayStart().AddMinutes(-1), "guldana", "qwen");
        Note("first-light", DayStart(), "guldana", "qwen");
        Note("next-day", DayStart(1), "guldana", "qwen");

        var today = await Report(DayArgument(0));
        var yesterday = await Report(DayArgument(-1));
        var tomorrow = await Report(DayArgument(1));

        today.ShouldContain("first-light");
        today.ShouldNotContain("before-midnight");
        today.ShouldNotContain("next-day");
        yesterday.ShouldContain("before-midnight");
        yesterday.ShouldNotContain("first-light");
        tomorrow.ShouldContain("next-day");
        tomorrow.ShouldNotContain("first-light");
    }

    [Fact]
    public async Task Report_ZeroMatches_PrintsOneExplicitEmptyLineCarryingWindowAndTags()
    {
        Note("other-day", DayStart(1).AddHours(3), "guldana", "qwen");
        Note("other-tag", DayStart().AddHours(4), "guldana", "qwen", tags: ["standup"]);

        var output = await Report(DayArgument());

        output.ShouldBe(
            $"No daily notes in {DayLabel()} (window {Window()}, tags daily-notes, tome default).{Environment.NewLine}");
    }

    [Fact]
    public async Task Report_DefaultTagIsDailyNotesAndExplicitTagOverrides()
    {
        await Report(DayArgument());
        _host.Store.ListedFilters.ShouldHaveSingleItem().Tags.ShouldBe(["daily-notes"]);

        await Report(DayArgument(), "--tag", "standup, retro,,");
        _host.Store.ListedFilters.Last().Tags.ShouldBe(["standup", "retro"]);
    }

    [Fact]
    public async Task Report_QueryCarriesActiveStatusProjectAndResolvedWindow()
    {
        Note("mine", DayStart().AddHours(5), "guldana", "qwen", projectId: "grimoire");

        var output = await Report(DayArgument(), "--project", "grimoire");

        var filter = _host.Store.ListedFilters.ShouldHaveSingleItem();
        filter.Status.ShouldBe(MemoryStatus.Active);
        filter.ProjectId.ShouldBe("grimoire");
        filter.CreatedAfter.ShouldBe(DayStart());
        filter.CreatedBefore.ShouldBe(DayStart(1));
        output.ShouldContain("mine");
        output.ShouldContain("Project: grimoire");
    }

    [Fact]
    public async Task Report_ProjectFilterDropsRecordsOfOtherProjects()
    {
        Note("mine", DayStart().AddHours(5), "guldana", "qwen", projectId: "grimoire");
        Note("theirs", DayStart().AddHours(6), "guldana", "qwen", projectId: "sandbox");

        var output = await Report(DayArgument(), "--project", "grimoire");

        output.ShouldContain("mine");
        output.ShouldNotContain("theirs");
    }

    [Fact]
    public async Task Report_WithoutDateDefaultsToTheLocalDayOfTheClock()
    {
        Note("today", DayStart().AddHours(9), "guldana", "qwen");

        var code = await _host.RunAsync("report");

        code.ShouldBe(ExitCodes.Ok);
        var filter = _host.Store.ListedFilters.ShouldHaveSingleItem();
        filter.CreatedAfter.ShouldBe(DayStart());
        filter.CreatedBefore.ShouldBe(DayStart(1));
        _host.Output.ToString().ShouldContain($"Daily notes {DayLabel()}");
    }

    [Fact]
    public async Task Report_NeverShowsSupersededOrTombstonedRecords()
    {
        Note("superseded-one", DayStart().AddHours(3), "guldana", "qwen", status: MemoryStatus.Superseded);
        Note("tombstoned-one", DayStart().AddHours(4), "guldana", "qwen", status: MemoryStatus.Tombstoned);
        Note("live-one", DayStart().AddHours(5), "guldana", "qwen");

        var output = await Report(DayArgument());

        output.ShouldContain("live-one");
        output.ShouldNotContain("superseded-one");
        output.ShouldNotContain("tombstoned-one");
    }

    [Fact]
    public async Task Report_JsonCarriesTheSameRecordIdsAsTheMarkdownOfTheSameQuery()
    {
        var first = Note("alpha-note", DayStart().AddHours(3), "alpha", "qwen", tags: ["daily-notes", "retro"]);
        var second = Note("beta-note", DayStart().AddHours(7), "beta", "claude");

        var markdown = await Report(DayArgument());

        var code = await _host.RunAsync("report", "--date", DayArgument(), "--json");

        code.ShouldBe(ExitCodes.Ok);
        using var document = JsonDocument.Parse(_host.Output.ToString());
        var root = document.RootElement;
        root.GetProperty("date").GetString().ShouldBe(DayLabel());
        root.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).ShouldBe(["daily-notes"]);
        root.GetProperty("total").GetInt32().ShouldBe(2);

        var actors = root.GetProperty("actors").EnumerateArray().ToArray();
        actors.Select(actor => actor.GetProperty("alias").GetString()).ShouldBe(["alpha", "beta"]);
        actors[1].GetProperty("vendor").GetString().ShouldBe("claude");

        var ids = actors
            .SelectMany(actor => actor.GetProperty("records").EnumerateArray())
            .Select(record => record.GetProperty("id").GetGuid())
            .ToList();
        ids.Count.ShouldBe(2);
        ids.ShouldContain(first.Id);
        ids.ShouldContain(second.Id);
        foreach (var id in ids)
        {
            markdown.ShouldContain(id.ToString());
        }

        markdown.ShouldContain("alpha-note");
        markdown.ShouldContain("beta-note");
    }

    [Fact]
    public async Task Report_JsonOfAnEmptyDayCarriesTheWindowAndNoActors()
    {
        var code = await _host.RunAsync("report", "--date", DayArgument(), "--json");

        code.ShouldBe(ExitCodes.Ok);
        using var document = JsonDocument.Parse(_host.Output.ToString());
        var root = document.RootElement;
        root.GetProperty("total").GetInt32().ShouldBe(0);
        root.GetProperty("actors").EnumerateArray().ShouldBeEmpty();
        root.GetProperty("window").GetProperty("from").GetString()
            .ShouldBe($"{DayStart():s}Z");
        root.GetProperty("window").GetProperty("to").GetString()
            .ShouldBe($"{DayStart(1):s}Z");
    }

    [Fact]
    public async Task Report_UnknownFlag_TakesTheUsageErrorPath()
    {
        var code = await _host.RunAsync("report", "--bogus", "x");

        code.ShouldBe(ExitCodes.Usage);
        var output = _host.Output.ToString();
        output.ShouldContain("Unknown option 'bogus' for 'report'.");
        output.ShouldContain("--date <yyyy-MM-dd>");
        output.ShouldNotContain("Unhandled exception");
    }

    [Fact]
    public async Task Report_MalformedDate_TakesTheUsageErrorPath()
    {
        var code = await _host.RunAsync("report", "--date", "yesterday");

        code.ShouldBe(ExitCodes.Usage);
        _host.Output.ToString().ShouldContain("--date must be a yyyy-MM-dd date or an ISO-8601 instant, got 'yesterday'.");
    }

    [Fact]
    public async Task Help_Report_ListsDateProjectTagAndJson()
    {
        var code = await _host.RunAsync("help", "report");

        code.ShouldBe(ExitCodes.Ok);
        var options = _host.Output.ToString();
        options.ShouldContain("--date <yyyy-MM-dd>");
        options.ShouldContain("--project <id>");
        options.ShouldContain("--tag <a,b>");
        options.ShouldContain("--json");
    }

    [Fact]
    public async Task Report_EchoesTheTomeItActuallyFilteredOn()
    {
        Note("today", DayStart().AddHours(9), "guldana", "qwen");

        var markdown = await Report(DayArgument());
        var filteredTome = _host.Store.ListedFilters.ShouldHaveSingleItem().Tome;
        filteredTome.ShouldBe("default");
        markdown.ShouldContain($"Tome: {filteredTome}", Case.Sensitive);

        var code = await _host.RunAsync("report", "--date", DayArgument(), "--json");

        code.ShouldBe(ExitCodes.Ok);
        using var document = JsonDocument.Parse(_host.Output.ToString());
        document.RootElement.GetProperty("tome").GetString()
            .ShouldBe(_host.Store.ListedFilters.Last().Tome);

        using var steed = new TestHost(
            variables: new Dictionary<string, string> { ["GRIMOIRE_TOME"] = "fieldwork" });
        Seed(steed, "field note", LocalDayStart(steed).AddHours(9), "guldana", "qwen", tome: "fieldwork");
        Seed(steed, "default-tome note", LocalDayStart(steed).AddHours(10), "guldana", "qwen");

        code = await steed.RunAsync("report", "--date", LocalDay(steed));

        code.ShouldBe(ExitCodes.Ok, steed.Output.ToString());
        var steedFiltered = steed.Store.ListedFilters.ShouldHaveSingleItem().Tome;
        steedFiltered.ShouldBe("fieldwork");
        var steedOutput = steed.Output.ToString();
        steedOutput.ShouldContain($"Tome: {steedFiltered}", Case.Sensitive);
        steedOutput.ShouldContain("field note");
        steedOutput.ShouldNotContain("default-tome note");
    }

    [Fact]
    public async Task Report_EnvTomeWinsOverConfigDefaultTome()
    {
        using var host = new TestHost(
            options: new MemoryEngineOptions { DefaultTome = "Crew" },
            variables: new Dictionary<string, string> { ["GRIMOIRE_TOME"] = "fieldwork" });
        Seed(host, "fieldwork note", LocalDayStart(host).AddHours(9), "guldana", "qwen", tome: "fieldwork");
        Seed(host, "crew note", LocalDayStart(host).AddHours(10), "guldana", "qwen", tome: "crew");

        var code = await host.RunAsync("report", "--date", LocalDay(host));

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        host.Store.ListedFilters.ShouldHaveSingleItem().Tome.ShouldBe("fieldwork");
        var output = host.Output.ToString();
        output.ShouldContain("Tome: fieldwork", Case.Sensitive);
        output.ShouldContain("fieldwork note");
        output.ShouldNotContain("crew note");
    }

    [Fact]
    public async Task Report_ReadsAndEchoesANonDefaultTome()
    {
        using var host = new TestHost(options: new MemoryEngineOptions { DefaultTome = "Crew" });
        Seed(host, "crew-note", LocalDayStart(host).AddHours(9), "guldana", "qwen", tome: "crew");
        Seed(host, "default-note", LocalDayStart(host).AddHours(10), "guldana", "qwen", tome: "default");

        var code = await host.RunAsync("report", "--date", LocalDay(host));

        code.ShouldBe(ExitCodes.Ok);
        host.Store.ListedFilters.ShouldHaveSingleItem().Tome.ShouldBe("crew");
        var output = host.Output.ToString();
        output.ShouldContain("crew-note");
        output.ShouldNotContain("default-note");
        output.ShouldContain("Tome: crew", Case.Sensitive);
    }

    public void Dispose() => _host.Dispose();

    private async Task<string> Report(string date, params string[] extra)
    {
        DrainOutput();
        var code = await _host.RunAsync(["report", "--date", date, .. extra]);
        code.ShouldBe(ExitCodes.Ok);
        return DrainOutput();
    }

    private string DrainOutput()
    {
        var text = _host.Output.ToString();
        _host.Output.GetStringBuilder().Clear();
        return text;
    }

    private MemoryRecord Note(
        string content,
        DateTimeOffset createdAt,
        string alias,
        string? vendor = null,
        string[]? tags = null,
        string? projectId = null,
        MemoryStatus status = MemoryStatus.Active) =>
        Seed(_host, content, createdAt, alias, vendor, tags, projectId, status);

    private static MemoryRecord Seed(
        TestHost host,
        string content,
        DateTimeOffset createdAt,
        string alias,
        string? vendor = null,
        string[]? tags = null,
        string? projectId = null,
        MemoryStatus status = MemoryStatus.Active,
        string? tome = null)
    {
        var record = new MemoryRecord
        {
            Id = Guid.NewGuid(),
            Content = content,
            Scope = MemoryScope.Project,
            Kind = MemoryKind.Journal,
            Actor = vendor is null ? new MemoryActor(alias, null) : MemoryActor.Agent(alias, vendor),
            Tome = tome ?? TomeKey.DefaultTome,
            CreatedAt = createdAt,
            LastAccessedAt = createdAt,
            Tags = tags ?? ["daily-notes"],
            ProjectId = projectId,
            Status = status,
        };
        host.Store.Seed(record, 1f, 0f);
        return record;
    }

    private DateTimeOffset DayStart(int offsetDays = 0) => LocalDayStart(_host, offsetDays);

    private static DateTimeOffset LocalDayStart(TestHost host, int offsetDays = 0)
    {
        var localDate = host.Clock.UtcNow.LocalDateTime.Date.AddDays(offsetDays);
        return new DateTimeOffset(localDate, TimeZoneInfo.Local.GetUtcOffset(localDate)).ToUniversalTime();
    }

    private static string LocalDay(TestHost host, int offsetDays = 0) =>
        LocalDayStart(host, offsetDays).LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private string DayArgument(int offsetDays = 0) => LocalDay(_host, offsetDays);

    private string DayLabel() => DayArgument();

    private string Window() => $"{DayStart():s}Z..{DayStart(1):s}Z";
}
