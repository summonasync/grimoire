using Grimoire.Core.Tests.Fakes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class BriefServiceTests
{
    private static readonly IReadOnlyDictionary<string, float[]> Vectors = new Dictionary<string, float[]>
    {
        ["anchor"] = [1f, 0f, 0f],
    };

    private readonly FakeMemoryStore _store = new();

    private readonly FakeClock _clock = new();

    private readonly FakeEmbeddingProvider _embeddings = new(Vectors);

    private readonly FakeTomeManifestStore _manifests = new();

    [Fact]
    public async Task Brief_EmptyStore_ReturnsExplicitEmptyStates()
    {
        var brief = await Service().BriefAsync(Query(), CancellationToken.None);

        brief.PersonaState.ShouldBe(BriefSectionState.Empty);
        brief.Persona.ShouldBeNull();
        brief.FactsState.ShouldBe(BriefSectionState.Empty);
        brief.Facts.ShouldBeEmpty();
        brief.TombstonesState.ShouldBe(BriefSectionState.Empty);
        brief.Tombstones.ShouldBeEmpty();
        brief.JournalState.ShouldBe(BriefSectionState.Empty);
        brief.Journal.ShouldBeEmpty();
        brief.Omitted.ShouldBeEmpty();
        brief.CompactionDue.ShouldBeNull();
    }

    [Fact]
    public async Task Brief_NoDailyFlag_DailyFieldsStayAtDefaultsOverPopulatedSections()
    {
        Seed("daily shape fact", kind: MemoryKind.Fact, importance: 1);
        Seed("daily shape journal", kind: MemoryKind.Journal, created: FakeClock.DefaultStart.AddHours(-1));

        var brief = await Service().BriefAsync(Query(), CancellationToken.None);

        brief.FactsState.ShouldBe(BriefSectionState.Populated);
        brief.JournalState.ShouldBe(BriefSectionState.Populated);
        brief.DailyState.ShouldBe(BriefSectionState.Empty);
        brief.DailyGroups.ShouldBeEmpty();
        brief.DailyMore.ShouldBe(0);
    }

    [Fact]
    public async Task Brief_StaleSimilarJournal_ReportsTheClusterSize()
    {
        SeedStaleJournal("wired serilog twice", [1f, 0.05f, 0f]);
        SeedStaleJournal("wired serilog again", [1f, 0.05f, 0.05f]);

        var brief = await Service().BriefAsync(Query(), CancellationToken.None);

        brief.CompactionDue.ShouldNotBeNull();
        brief.CompactionDue!.PendingEntries.ShouldBe(2);
    }

    [Fact]
    public async Task Brief_BelowMinClusterStaleness_ReportsNoCompactionDue()
    {
        Seed("fresh note", kind: MemoryKind.Journal);
        SeedStaleJournal("lone aging note", [1f, 0f, 0f]);

        var brief = await Service().BriefAsync(Query(), CancellationToken.None);

        brief.CompactionDue.ShouldBeNull();
    }

    [Fact]
    public async Task Brief_StaleButDissimilarJournal_ReportsNoCompactionDue()
    {
        SeedStaleJournal("orthogonally unrelated one", [1f, 0f, 0f]);
        SeedStaleJournal("orthogonally unrelated two", [0f, 1f, 0f]);

        var brief = await Service().BriefAsync(Query(), CancellationToken.None);

        brief.CompactionDue.ShouldBeNull();
    }

    [Fact]
    public async Task Brief_PersonalStaleJournal_CountsIntoTheBacklogTheJournalSectionHides()
    {
        SeedStaleJournal("personal aging one", [1f, 0.05f, 0f], MemoryScope.Personal);
        SeedStaleJournal("personal aging two", [1f, 0.05f, 0.05f], MemoryScope.Personal);

        var brief = await Service().BriefAsync(Query(scopes: [MemoryScope.Project, MemoryScope.Global]), CancellationToken.None);

        brief.Journal.ShouldBeEmpty();
        brief.CompactionDue!.PendingEntries.ShouldBe(2);
    }

    [Fact]
    public async Task Brief_CompactionDueFollowsTheBriefedTome_NotTheDefaultOne()
    {
        SeedStaleJournal("default tome twin one", [1f, 0.05f, 0f]);
        SeedStaleJournal("default tome twin two", [1f, 0.05f, 0.05f]);
        SeedStaleJournal("alpha lone aging note", [1f, 0f, 0f], tome: "alpha");

        var briefed = await Service().BriefAsync(Query(tome: "alpha"), CancellationToken.None);
        var owner = await Service().BriefAsync(Query(), CancellationToken.None);

        briefed.CompactionDue.ShouldBeNull();
        owner.CompactionDue!.PendingEntries.ShouldBe(2);
    }

    [Fact]
    public async Task Brief_FactsOrderRecencyDecayAndImportance_WithoutQueryVector()
    {
        Seed("vital", kind: MemoryKind.Fact, importance: 1, accessed: FakeClock.DefaultStart);
        Seed("mid", kind: MemoryKind.Fact, importance: 0.6, accessed: FakeClock.DefaultStart);
        Seed("weak", kind: MemoryKind.Fact, importance: 0.2, accessed: FakeClock.DefaultStart);
        Seed("stale", kind: MemoryKind.Fact, importance: 1, accessed: FakeClock.DefaultStart.AddDays(-30));

        var brief = await Service().BriefAsync(Query(), CancellationToken.None);

        brief.FactsState.ShouldBe(BriefSectionState.Populated);
        brief.Facts.Select(fact => fact.Content).ShouldBe(["vital", "mid", "weak", "stale"]);
        brief.Facts[0].Score.ShouldBe(1.0, 1e-3);
        brief.Facts[1].Score.ShouldBe(0.8, 1e-3);
        brief.Facts[2].Score.ShouldBe(0.6, 1e-3);
        brief.Facts[3].Score.ShouldBe(0.5, 1e-3);
    }

    [Fact]
    public async Task Brief_JournalCapsEntriesAndMarksTruncation()
    {
        Seed("old note", kind: MemoryKind.Journal, created: FakeClock.DefaultStart.AddHours(-3));
        Seed(new string('x', 500), kind: MemoryKind.Journal, created: FakeClock.DefaultStart.AddHours(-1));
        Seed("mid note", kind: MemoryKind.Journal, created: FakeClock.DefaultStart.AddHours(-2));

        var brief = await Service().BriefAsync(Query(), CancellationToken.None);

        brief.JournalState.ShouldBe(BriefSectionState.Populated);
        brief.Journal.Count.ShouldBe(3);
        brief.Journal[0].Content.ShouldBe(new string('x', 400));
        brief.Journal[0].Truncated.ShouldBeTrue();
        brief.Journal[1].Content.ShouldBe("mid note");
        brief.Journal[1].Truncated.ShouldBeFalse();
        brief.Journal[2].Content.ShouldBe("old note");
        brief.Journal[2].Truncated.ShouldBeFalse();
    }

    [Fact]
    public async Task Brief_TombstonesCarryReferencesNewestFirst()
    {
        var first = Seed("stale decision", status: MemoryStatus.Tombstoned, tombstoneReason: "superseded by vote", tombstonedAt: FakeClock.DefaultStart.AddDays(-5));
        var latest = Seed("fresh decision", status: MemoryStatus.Tombstoned, tombstoneReason: "wrong fact", tombstonedAt: FakeClock.DefaultStart.AddDays(-1));

        var brief = await Service().BriefAsync(Query(), CancellationToken.None);

        brief.TombstonesState.ShouldBe(BriefSectionState.Populated);
        brief.Tombstones.Count.ShouldBe(2);
        brief.Tombstones[0].Id.ShouldBe(latest.Id);
        brief.Tombstones[0].Reason.ShouldBe("wrong fact");
        brief.Tombstones[0].TombstonedAt.ShouldBe(FakeClock.DefaultStart.AddDays(-1));
        brief.Tombstones[1].Id.ShouldBe(first.Id);
        brief.Tombstones[1].Reason.ShouldBe("superseded by vote");
    }

    [Fact]
    public async Task Brief_TombstonesDefaultToTenNewest()
    {
        for (var day = 0; day < 12; day++)
        {
            Seed($"buried {day}", status: MemoryStatus.Tombstoned, tombstoneReason: $"reason {day}", tombstonedAt: FakeClock.DefaultStart.AddDays(-day));
        }

        var brief = await Service().BriefAsync(Query(), CancellationToken.None);

        brief.Tombstones.Count.ShouldBe(10);
        brief.Tombstones[0].Reason.ShouldBe("reason 0");
        brief.Tombstones[9].Reason.ShouldBe("reason 9");
        brief.Tombstones.Select(tombstone => tombstone.TombstonedAt).ShouldBeInOrder(Shouldly.SortDirection.Descending);
    }

    [Fact]
    public async Task Brief_SectionOverBudget_IsOmittedAndCapHolds()
    {
        SeedPersona(new string('p', 100));
        Seed(new string('f', 80), kind: MemoryKind.Fact);
        Seed(new string('g', 80), kind: MemoryKind.Fact);
        Seed("buried", status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: FakeClock.DefaultStart);
        Seed(new string('j', 60), kind: MemoryKind.Journal, created: FakeClock.DefaultStart.AddHours(-1));
        Seed(new string('k', 60), kind: MemoryKind.Journal, created: FakeClock.DefaultStart.AddHours(-2));

        var options = new MemoryEngineOptions { BriefMaxTotalChars = 250 };
        var brief = await Service(options).BriefAsync(Query(), CancellationToken.None);

        brief.PersonaState.ShouldBe(BriefSectionState.Populated);
        // facts take whole entries while the purse holds instead of dropping the section wholesale
        brief.FactsState.ShouldBe(BriefSectionState.Populated);
        brief.Facts.ShouldNotBeEmpty();
        brief.Facts.Sum(fact => fact.Content.Length).ShouldBeLessThanOrEqualTo(250);
        brief.TombstonesState.ShouldBe(BriefSectionState.Populated);
        brief.Omitted.ShouldNotContain(BriefSection.Facts);
        // what the facts purse left behind is journal's problem now — the all-or-nothing Fit applies there
        brief.JournalState.ShouldBe(BriefSectionState.Omitted);
        brief.Omitted.ShouldBe([BriefSection.Journal]);

        var consumed = brief.Persona!.Identity.Length
            + brief.Facts.Sum(fact => fact.Content.Length)
            + brief.Tombstones.Sum(tombstone => tombstone.Reason!.Length)
            + brief.Journal.Sum(entry => entry.Content.Length);
        consumed.ShouldBeLessThanOrEqualTo(250);
    }

    [Fact]
    public async Task Brief_FactBiggerThanTheWholePurse_IsOmittedAndNamed()
    {
        Seed(new string('f', 80), kind: MemoryKind.Fact);

        var brief = await Service(new MemoryEngineOptions { BriefMaxTotalChars = 40 })
            .BriefAsync(Query(), CancellationToken.None);

        brief.FactsState.ShouldBe(BriefSectionState.Omitted);
        brief.Facts.ShouldBeEmpty();
        brief.Omitted.ShouldBe([BriefSection.Facts]);
        var section = brief.OmissionDigest![BriefSection.Facts];
        section.Count.ShouldBe(1);
        section.Dropped.Single().Reason.ShouldBe(BriefOmissionReason.SectionBudget);
    }

    [Fact]
    public async Task Brief_FactsSection_SkipsAnOversizedFactAndKeepsTheNextThatFits()
    {
        Seed(new string('h', 90), kind: MemoryKind.Fact, importance: 1);
        Seed(new string('s', 30), kind: MemoryKind.Fact, importance: 0.1, created: FakeClock.DefaultStart.AddHours(-1));

        var brief = await Service(new MemoryEngineOptions { BriefMaxTotalChars = 60 })
            .BriefAsync(Query(), CancellationToken.None);

        brief.FactsState.ShouldBe(BriefSectionState.Populated);
        brief.Facts.Select(fact => fact.Content).ShouldBe([new string('s', 30)]);

        var section = brief.OmissionDigest![BriefSection.Facts];
        section.Count.ShouldBe(1);
        section.Dropped.Single().Reason.ShouldBe(BriefOmissionReason.SectionBudget);
    }

    [Fact]
    public async Task Brief_PersonaOverBudget_IsOmittedAndLaterSectionsFill()
    {
        SeedPersona(new string('p', 500));
        Seed(new string('f', 50), kind: MemoryKind.Fact);

        var options = new MemoryEngineOptions { BriefMaxTotalChars = 100 };
        var brief = await Service(options).BriefAsync(Query(), CancellationToken.None);

        brief.PersonaState.ShouldBe(BriefSectionState.Omitted);
        brief.Persona.ShouldBeNull();
        brief.FactsState.ShouldBe(BriefSectionState.Populated);
        brief.Facts.Single().Content.ShouldBe(new string('f', 50));
        brief.Omitted.ShouldBe([BriefSection.Persona]);
    }

    [Fact]
    public async Task Brief_SessionScopeIsExcludedEvenWhenRequested()
    {
        Seed("session fact", kind: MemoryKind.Fact, scope: MemoryScope.Session, sessionId: "s-1");
        Seed("session journal", kind: MemoryKind.Journal, scope: MemoryScope.Session, sessionId: "s-1");
        Seed("project fact", kind: MemoryKind.Fact);

        var brief = await Service().BriefAsync(Query(scopes: [MemoryScope.Session]), CancellationToken.None);

        brief.Facts.Select(fact => fact.Content).ShouldBe(["project fact"]);
        brief.Journal.ShouldBeEmpty();
    }

    [Fact]
    public async Task Brief_SupersededAndTombstonedNeverLeakIntoFactsOrJournal()
    {
        Seed("superseded fact", kind: MemoryKind.Fact, status: MemoryStatus.Superseded, supersededBy: Guid.NewGuid());
        Seed("tombstoned fact", kind: MemoryKind.Fact, status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: FakeClock.DefaultStart);
        Seed("tombstoned journal", kind: MemoryKind.Journal, status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: FakeClock.DefaultStart);
        Seed("active fact", kind: MemoryKind.Fact);
        Seed("active journal", kind: MemoryKind.Journal);

        var brief = await Service().BriefAsync(Query(), CancellationToken.None);

        brief.Facts.Select(fact => fact.Content).ShouldBe(["active fact"]);
        brief.Journal.Select(entry => entry.Content).ShouldBe(["active journal"]);
    }

    [Fact]
    public async Task Brief_PersonaRecordsStayOutOfFacts()
    {
        SeedPersona("you are a grimoire keeper");
        Seed("plain fact", kind: MemoryKind.Fact);

        var brief = await Service().BriefAsync(Query(), CancellationToken.None);

        brief.PersonaState.ShouldBe(BriefSectionState.Populated);
        brief.Persona!.Identity.ShouldBe("you are a grimoire keeper");
        brief.Facts.Select(fact => fact.Content).ShouldBe(["plain fact"]);
    }

    [Fact]
    public async Task Brief_LimitHintsApplyPerScopeAndClamp()
    {
        Seed("fact one", kind: MemoryKind.Fact, importance: 1);
        Seed("fact two", kind: MemoryKind.Fact, importance: 0.8);
        Seed("fact three", kind: MemoryKind.Fact, importance: 0.6);
        Seed("global fact", kind: MemoryKind.Fact, scope: MemoryScope.Global, importance: 0.4);

        var brief = await Service().BriefAsync(Query(factsPerScope: 2), CancellationToken.None);

        brief.Facts.Count.ShouldBe(3);
        brief.Facts.Take(2).Select(fact => fact.Content).ShouldBe(["fact one", "fact two"]);
        brief.Facts.Last().Content.ShouldBe("global fact");

        var clamped = await Service().BriefAsync(Query(factsPerScope: 0), CancellationToken.None);

        clamped.Facts.Count.ShouldBe(2);
        clamped.Facts.Select(fact => fact.Content).ShouldBe(["fact one", "global fact"]);
    }

    [Fact]
    public async Task Brief_JournalLimitHintTakesNewestPerScope()
    {
        Seed("journal one", kind: MemoryKind.Journal, created: FakeClock.DefaultStart.AddHours(-1));
        Seed("journal two", kind: MemoryKind.Journal, created: FakeClock.DefaultStart.AddHours(-2));
        Seed("journal three", kind: MemoryKind.Journal, created: FakeClock.DefaultStart.AddHours(-3));

        var brief = await Service().BriefAsync(Query(journalPerScope: 2), CancellationToken.None);

        brief.Journal.Select(entry => entry.Content).ShouldBe(["journal one", "journal two"]);
    }

    [Fact]
    public async Task Brief_ProjectFilterNarrowsAllSections()
    {
        Seed("foreign fact", kind: MemoryKind.Fact, projectId: "other");
        Seed("own fact", kind: MemoryKind.Fact, projectId: "grim");
        Seed("buried", status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: FakeClock.DefaultStart, projectId: "other");

        var brief = await Service().BriefAsync(Query(projectId: "grim"), CancellationToken.None);

        brief.Facts.Select(fact => fact.Content).ShouldBe(["own fact"]);
        brief.Tombstones.ShouldBeEmpty();
    }

    [Fact]
    public async Task Brief_DefaultScopesExcludePersonal_ExplicitScopesMayIncludeIt()
    {
        Seed("personal note", kind: MemoryKind.Fact, scope: MemoryScope.Personal);

        var brief = await Service().BriefAsync(Query(), CancellationToken.None);

        brief.FactsState.ShouldBe(BriefSectionState.Empty);
        brief.Facts.ShouldBeEmpty();

        var widened = await Service().BriefAsync(Query(scopes: [MemoryScope.Personal]), CancellationToken.None);

        widened.Facts.Select(fact => fact.Content).ShouldBe(["personal note"]);
    }

    [Fact]
    public async Task Brief_BindsPersonaAndSectionsToResolvedTome()
    {
        Seed("shared fact", kind: MemoryKind.Fact, tome: "alpha");
        Seed("shared journal", kind: MemoryKind.Journal, tome: "beta");

        var alpha = await Service().BriefAsync(Query(tome: "alpha"), CancellationToken.None);

        alpha.Facts.Select(fact => fact.Content).ShouldBe(["shared fact"]);
        alpha.Journal.ShouldBeEmpty();

        var beta = await Service().BriefAsync(Query(tome: "beta"), CancellationToken.None);

        beta.Facts.ShouldBeEmpty();
        beta.Journal.Select(entry => entry.Content).ShouldBe(["shared journal"]);
    }

    [Fact]
    public async Task Brief_DailyFlag_FourActorsEightNotesProduceFourGroupsOldestFirst()
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "medivh pushed the brief", Utc(2026, 11, 8, 22), "medivh", "claude");
        SeedDailyNote(store, "medivh opened the board", Utc(2026, 11, 9, 5), "medivh", "qwen");
        SeedDailyNote(store, "medivh closed the gate", Utc(2026, 11, 9, 6, 30), "medivh", "claude");
        SeedDailyNote(store, "thrall wired serilog", Utc(2026, 11, 9, 4), "thrall", "codex");
        SeedDailyNote(store, "thrall fixed the port", Utc(2026, 11, 9, 5, 30), "thrall", "codex");
        SeedDailyNote(store, "kelthuzad reviewed t1", Utc(2026, 11, 6, 8), "kelthuzad", "claude");
        SeedDailyNote(store, "kelthuzad logged ac", Utc(2026, 11, 6, 9), "kelthuzad", "claude");
        SeedDailyNote(store, "dagget slept", Utc(2026, 11, 6, 10), "dagget", "qwen");

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions())
            .BriefAsync(Query(daily: true), CancellationToken.None);

        brief.DailyState.ShouldBe(BriefSectionState.Populated);
        brief.DailyMore.ShouldBe(0);
        brief.DailyGroups.Select(group => group.Entries.Count).Sum().ShouldBe(8);
        brief.DailyGroups
            .Select(group => (group.Bucket, group.Actor))
            .ToList()
            .ShouldBe(
            [
                (BriefDailyBucket.LastWorkingDay, "dagget"),
                (BriefDailyBucket.LastWorkingDay, "kelthuzad"),
                (BriefDailyBucket.Today, "medivh"),
                (BriefDailyBucket.Today, "thrall"),
            ]);
        brief.DailyGroups[0].Day.ShouldBe(new DateOnly(2026, 11, 6));
        brief.DailyGroups[2].Day.ShouldBe(new DateOnly(2026, 11, 9));
        brief.DailyGroups[2].Entries.Select(entry => entry.Content).ToList().ShouldBe(
            ["medivh pushed the brief", "medivh opened the board", "medivh closed the gate"]);
        brief.DailyGroups[3].Entries.Select(entry => entry.Content).ToList().ShouldBe(
            ["thrall wired serilog", "thrall fixed the port"]);
    }

    [Fact]
    public async Task Brief_DailyFlag_NoRecordIdAppearsInBothDailyAndJournal()
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "daily overlapping note", Utc(2026, 11, 9, 6), "medivh", "claude");
        SeedIn(store, "plain journal note", created: Utc(2026, 11, 9, 5));
        SeedIn(store, "older journal note", created: Utc(2026, 11, 4, 5));

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions())
            .BriefAsync(Query(daily: true), CancellationToken.None);

        var dailyIds = brief.DailyGroups.SelectMany(group => group.Entries).Select(entry => entry.Id).ToList();
        dailyIds.ShouldHaveSingleItem();
        brief.Journal.Select(entry => entry.Id).Intersect(dailyIds).ShouldBeEmpty();
        brief.Journal.Select(entry => entry.Content).ToList().ShouldBe(["plain journal note", "older journal note"]);
    }

    [Fact]
    public async Task Brief_DailyFlag_JournalKeepsItsPerScopeCountOnADailyHeavyDay()
    {
        var store = new FakeMemoryStore();
        for (var index = 0; index < 5; index++)
        {
            SeedDailyNote(store, $"daily note {index}", Utc(2026, 11, 9, 3 + index), "medivh", "claude");
        }

        SeedIn(store, "plain newest", created: Utc(2026, 11, 9, 2));
        SeedIn(store, "plain second", created: Utc(2026, 11, 8, 23));
        SeedIn(store, "plain oldest", created: Utc(2026, 11, 7, 6));

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions())
            .BriefAsync(Query(daily: true, journalPerScope: 2), CancellationToken.None);

        brief.DailyGroups.SelectMany(group => group.Entries).Count().ShouldBe(5);
        brief.Journal.Select(entry => entry.Content).ToList().ShouldBe(["plain newest", "plain second"]);
    }

    [Fact]
    public async Task Brief_DailyFlag_OversizedBodyIsCappedFlaggedAndNamesDailyInOmitted()
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "short daily note", Utc(2026, 11, 9, 4), "medivh", "claude");
        SeedDailyNote(store, new string('x', 2000), Utc(2026, 11, 9, 5), "medivh", "claude");

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions())
            .BriefAsync(Query(daily: true), CancellationToken.None);

        var entries = brief.DailyGroups.SelectMany(group => group.Entries).ToList();
        entries.Count.ShouldBe(2);
        entries[0].Truncated.ShouldBeFalse();
        entries[0].Content.ShouldBe("short daily note");
        entries[1].Truncated.ShouldBeTrue();
        entries[1].Content.Length.ShouldBe(1500);
        brief.DailyMore.ShouldBe(0);
        brief.Omitted.Count(section => section == BriefSection.Daily).ShouldBe(1);
    }

    [Fact]
    public async Task Brief_DailyFlag_OverBudgetTailIsCountedInDailyMoreAndNamedInOmitted()
    {
        var store = new FakeMemoryStore();
        for (var index = 0; index < 4; index++)
        {
            SeedDailyNote(store, new string((char)('a' + index), 40), Utc(2026, 11, 9, 3 + index), "medivh", "claude");
        }

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions(totalCap: 100))
            .BriefAsync(Query(daily: true), CancellationToken.None);

        brief.DailyState.ShouldBe(BriefSectionState.Populated);
        brief.DailyGroups.ShouldHaveSingleItem();
        brief.DailyGroups[0].Entries.Select(entry => entry.Content).ToList().ShouldBe(
            [new string('a', 40), new string('b', 40)]);
        brief.DailyGroups.SelectMany(group => group.Entries).ShouldAllBe(entry => !entry.Truncated);
        brief.DailyMore.ShouldBe(2);
        brief.Omitted.ShouldBe([BriefSection.Daily]);
    }

    [Fact]
    public async Task Brief_DailyFlag_CaseVariantsOfOneAliasCollapseIntoOneGroup()
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "radim older note", Utc(2026, 11, 9, 4), "Radim", "claude");
        SeedDailyNote(store, "radim newer note", Utc(2026, 11, 9, 5), "radim", "qwen");
        SeedDailyNote(store, "zulin one note", Utc(2026, 11, 9, 6), "Zulin", "claude");

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions())
            .BriefAsync(Query(daily: true), CancellationToken.None);

        brief.DailyGroups.Count.ShouldBe(2);
        brief.DailyGroups.Select(group => group.Actor).ToList().ShouldBe(["Radim", "Zulin"]);
        brief.DailyGroups[0].Entries.Select(entry => entry.Content).ToList()
            .ShouldBe(["radim older note", "radim newer note"]);
    }

    [Fact]
    public async Task Brief_DailyFlag_NeverConsumesTheSharedBriefBudget()
    {
        var store = new FakeMemoryStore();
        SeedIn(store, new string('f', 50), kind: MemoryKind.Fact);
        SeedIn(store, new string('j', 30), created: Utc(2026, 11, 9, 4));
        SeedDailyNote(store, new string('d', 400), Utc(2026, 11, 9, 5), "medivh", "claude");
        _manifests.Seed(new TomeManifest
        {
            Key = "alpha",
            Description = "the alpha tome",
            CreatedBy = "medivh",
            CreatedAt = Utc(2026, 11, 1, 9),
            UpdatedAt = Utc(2026, 11, 1, 9),
        });

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions() with { BriefMaxTotalChars = 200 })
            .BriefAsync(Query(daily: true), CancellationToken.None);

        brief.FactsState.ShouldBe(BriefSectionState.Populated);
        brief.JournalState.ShouldBe(BriefSectionState.Populated);
        brief.TomesState.ShouldBe(BriefSectionState.Populated);
        brief.DailyState.ShouldBe(BriefSectionState.Populated);
        brief.Omitted.ShouldBeEmpty();
        brief.Journal.Select(entry => entry.Content).ToList().ShouldBe([new string('j', 30)]);
        brief.DailyGroups.SelectMany(group => group.Entries).Single().Content.Length.ShouldBe(400);
        brief.DailyMore.ShouldBe(0);
    }

    [Fact]
    public async Task Brief_DailyFlag_PinsTheExactStoreFilterFormForEachWindowRead()
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "today note", Utc(2026, 11, 9, 5), "medivh", "claude");
        SeedDailyNote(store, "friday note", Utc(2026, 11, 6, 8), "kelthuzad", "claude");
        SeedIn(store, "plain journal note", created: Utc(2026, 11, 9, 4));

        var captured = new List<MemoryFilter>();
        store.OnListAsync = captured.Add;

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions())
            .BriefAsync(Query(daily: true), CancellationToken.None);

        // the hook fires at the top of ListAsync, so capture order is call order, exactly as the old
        // per-call list was; window bounds are what separate the daily reads from the persona read,
        // which also arrives carrying a tag
        var dailyFilters = captured
            .Where(filter => filter.CreatedAfter is not null || filter.CreatedBefore is not null)
            .ToArray();
        dailyFilters.Length.ShouldBe(4);

        var expectedDays = new[] { new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 8), new DateOnly(2026, 11, 7), new DateOnly(2026, 11, 6) };
        for (var index = 0; index < dailyFilters.Length; index++)
        {
            var filter = dailyFilters[index];
            filter.Tags.ShouldBe(["daily-notes"]);
            filter.Kind.ShouldBe(MemoryKind.Journal);
            filter.Status.ShouldBe(MemoryStatus.Active);
            filter.Tome.ShouldBe("default");
            filter.ProjectId.ShouldBeNull();
            filter.Scopes.ShouldBe([MemoryScope.Project, MemoryScope.Global]);
            filter.ExcludeTags.ShouldBeEmpty();
            filter.CreatedAfter.ShouldBe(MoscowMidnightUtc(expectedDays[index]));
            filter.CreatedBefore.ShouldBe(MoscowMidnightUtc(expectedDays[index]).AddDays(1));
            filter.CreatedAfter!.Value.Offset.ShouldBe(TimeSpan.Zero);
            filter.CreatedBefore!.Value.Offset.ShouldBe(TimeSpan.Zero);
        }

        brief.DailyState.ShouldBe(BriefSectionState.Populated);
    }

    [Fact]
    public async Task Brief_DailyFlag_NoteBeyondTheWindowUpperBoundNeverLeaksIntoAWindow()
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "today note", Utc(2026, 11, 9, 5), "medivh", "claude");
        SeedDailyNote(store, "note two local days ahead", Utc(2026, 11, 11, 5), "medivh", "claude");

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions())
            .BriefAsync(Query(daily: true), CancellationToken.None);

        brief.DailyGroups.ShouldHaveSingleItem();
        brief.DailyGroups[0].Bucket.ShouldBe(BriefDailyBucket.Today);
        brief.DailyGroups[0].Entries.Select(entry => entry.Content).ToList().ShouldBe(["today note"]);
    }

    [Fact]
    public async Task Brief_DailyFlag_SaturdayAndSundayEmptyResolveLastWorkingDayToFriday()
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "kelthuzad reviewed t1", Utc(2026, 11, 6, 8), "kelthuzad", "claude");
        SeedDailyNote(store, "kelthuzad logged ac", Utc(2026, 11, 6, 9), "kelthuzad", "claude");
        SeedDailyNote(store, "dagget slept", Utc(2026, 11, 6, 10), "dagget", "qwen");

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions())
            .BriefAsync(Query(daily: true), CancellationToken.None);

        brief.DailyState.ShouldBe(BriefSectionState.Populated);
        brief.DailyGroups.ShouldAllBe(group => group.Bucket == BriefDailyBucket.LastWorkingDay);
        brief.DailyGroups.Select(group => group.Day).Distinct().ShouldHaveSingleItem().ShouldBe(new DateOnly(2026, 11, 6));
        brief.DailyGroups.SelectMany(group => group.Entries).Count().ShouldBe(3);
        brief.DailyMore.ShouldBe(0);
    }

    [Fact]
    public async Task Brief_DailyFlag_NoteOnTheOldestCandidateStillSurfaces()
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "seven days back note", Utc(2026, 11, 2, 6), "medivh", "claude");

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions())
            .BriefAsync(Query(daily: true), CancellationToken.None);

        brief.DailyGroups.ShouldHaveSingleItem();
        brief.DailyGroups[0].Bucket.ShouldBe(BriefDailyBucket.LastWorkingDay);
        brief.DailyGroups[0].Day.ShouldBe(new DateOnly(2026, 11, 2));
    }

    [Fact]
    public async Task Brief_DailyFlag_NoteEightLocalDaysBackYieldsEmptySectionNotStale()
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "eight days back note", Utc(2026, 11, 1, 6), "medivh", "claude");

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions())
            .BriefAsync(Query(daily: true), CancellationToken.None);

        brief.DailyState.ShouldBe(BriefSectionState.Empty);
        brief.DailyGroups.ShouldBeEmpty();
        brief.DailyMore.ShouldBe(0);
        brief.Omitted.ShouldNotContain(BriefSection.Daily);
    }

    [Fact]
    public async Task Brief_DailyFlag_WithoutZoneTheEmittedDayIsTheHostZoneCalendarDay()
    {
        var now = new DateTimeOffset(2026, 11, 12, 22, 0, 0, TimeSpan.Zero);
        var hostDay = LocalDayOf(now, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneInfo.Local.Id));
        var utcDay = LocalDayOf(now, TimeZoneInfo.Utc);
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "thirty minutes before the clock", now.AddMinutes(-30), "medivh", "claude");

        var brief = await ServiceAt(now, store, new MemoryEngineOptions())
            .BriefAsync(Query(daily: true), CancellationToken.None);

        brief.DailyGroups.ShouldHaveSingleItem();
        brief.DailyGroups[0].Bucket.ShouldBe(BriefDailyBucket.Today);
        brief.DailyGroups[0].Day.ShouldBe(hostDay, "the day boundary must come from the zone the resolver reports for an unset key, never from a hardcoded UTC day");

        // Only a host whose zone moves the calendar day can distinguish the two routes; on a
        // UTC-local host the values coincide and this half of the claim is unfalsifiable by
        // construction, so it is asserted as a gate rather than skipped silently.
        if (hostDay != utcDay)
        {
            brief.DailyGroups[0].Day.ShouldNotBe(utcDay);
        }
    }

    [Fact]
    public async Task Brief_DailyFlag_ExplicitZoneOverridesTheHostDefault()
    {
        var now = new DateTimeOffset(2026, 11, 12, 22, 0, 0, TimeSpan.Zero);
        var hostDay = LocalDayOf(now, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneInfo.Local.Id));
        var utcDay = LocalDayOf(now, TimeZoneInfo.Utc);
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "thirty minutes before the clock", now.AddMinutes(-30), "medivh", "claude");

        var pinned = await ServiceAt(now, store, new MemoryEngineOptions { DailyTimeZone = "UTC" })
            .BriefAsync(Query(daily: true), CancellationToken.None);
        var defaulted = await ServiceAt(now, store, new MemoryEngineOptions())
            .BriefAsync(Query(daily: true), CancellationToken.None);

        pinned.DailyGroups.ShouldHaveSingleItem();
        pinned.DailyGroups[0].Day.ShouldBe(utcDay);
        defaulted.DailyGroups.ShouldHaveSingleItem();
        defaulted.DailyGroups[0].Day.ShouldBe(hostDay);
        if (hostDay != utcDay)
        {
            defaulted.DailyGroups[0].Day.ShouldNotBe(pinned.DailyGroups[0].Day);
        }
    }

    [Fact]
    public async Task Brief_DailyFlag_UnresolvableConfiguredZoneWarnsOnceNamingTheId()
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "today note", Utc(2026, 11, 9, 5), "medivh", "claude");
        SeedDailyNote(store, "friday note", Utc(2026, 11, 6, 8), "kelthuzad", "claude");

        var windowReads = 0;
        store.OnListAsync = filter =>
        {
            if (filter.CreatedAfter is not null)
            {
                windowReads++;
            }
        };

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions(zone: "Not/AZone"))
            .BriefAsync(Query(daily: true), CancellationToken.None);

        brief.DailyState.ShouldBe(BriefSectionState.Populated);
        windowReads.ShouldBe(4);
        brief.Warnings.ShouldHaveSingleItem();

        var warning = brief.Warnings![0];
        warning.ShouldBe("DailyTimeZone 'Not/AZone' did not resolve; daily buckets use UTC.");
        warning.ShouldContain("Not/AZone");
        warning.ShouldContain("UTC");
        warning.ShouldNotContain("Exception");
        warning.ShouldNotContain("Could not find");

        CanonicalPayload(brief).ShouldContain(
            "\"Warnings\":[\"DailyTimeZone 'Not/AZone' did not resolve; daily buckets use UTC.\"]");
    }

    [Fact]
    public async Task Brief_DailyFlag_UnsetZoneStaysSilentWhileTheSameClockWithABogusIdWarns()
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "today note", Utc(2026, 11, 9, 5), "medivh", "claude");

        var windowReads = 0;
        store.OnListAsync = filter =>
        {
            if (filter.CreatedAfter is not null)
            {
                windowReads++;
            }
        };

        var defaulted = await ServiceAt(MoscowMondayUtc, store, DailyOptions(zone: null))
            .BriefAsync(Query(daily: true), CancellationToken.None);

        windowReads.ShouldBeGreaterThan(0);
        defaulted.Warnings.ShouldBeNull();

        var bogus = await ServiceAt(MoscowMondayUtc, store, DailyOptions(zone: "Not/AZone"))
            .BriefAsync(Query(daily: true), CancellationToken.None);

        bogus.Warnings.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Brief_DailyFlag_ResolvableConfiguredZoneStaysSilentWhileABogusIdWarns()
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "today note", Utc(2026, 11, 9, 5), "medivh", "claude");

        var moscow = await ServiceAt(MoscowMondayUtc, store, DailyOptions(zone: "Europe/Moscow"))
            .BriefAsync(Query(daily: true), CancellationToken.None);

        moscow.DailyState.ShouldBe(BriefSectionState.Populated);
        moscow.DailyGroups.ShouldHaveSingleItem();
        moscow.Warnings.ShouldBeNull();

        var bogus = await ServiceAt(MoscowMondayUtc, store, DailyOptions(zone: "Not/AZone"))
            .BriefAsync(Query(daily: true), CancellationToken.None);

        bogus.Warnings.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Brief_FlagOff_WithUnresolvableZoneNeverReachesTheDailyWarningPath()
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "today note", Utc(2026, 11, 9, 5), "medivh", "claude");

        var windowReads = 0;
        store.OnListAsync = filter =>
        {
            if (filter.CreatedAfter is not null)
            {
                windowReads++;
            }
        };

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions(zone: "Not/AZone"))
            .BriefAsync(Query(), CancellationToken.None);

        windowReads.ShouldBe(0);
        brief.Warnings.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Brief_DailyFlag_BlankTagWarnsOnceAndLeavesTheSectionEmpty(string tag)
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "medivh opened the board", Utc(2026, 11, 9, 5), "medivh", "claude");
        SeedDailyNote(store, "thrall wired the door", Utc(2026, 11, 9, 6), "thrall", "claude");

        var windowReads = 0;
        store.OnListAsync = filter =>
        {
            if (filter.CreatedAfter is not null)
            {
                windowReads++;
            }
        };

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions(tag: tag))
            .BriefAsync(Query(daily: true), CancellationToken.None);

        store.Records.Count(record => record.Tags.Contains("daily-notes")).ShouldBe(2);
        windowReads.ShouldBeGreaterThan(0);

        var warnings = brief.Warnings!.ToArray();
        warnings.Length.ShouldBe(1);
        warnings[0].ShouldBe("DailyTag is empty; the daily section is disabled. Set DailyTag to enable it.");
        warnings[0].ShouldContain("DailyTag");
        warnings[0].ShouldNotContain("DailyTimeZone");
        brief.DailyState.ShouldBe(BriefSectionState.Empty);
        brief.DailyGroups.ShouldBeEmpty();
        brief.DailyMore.ShouldBe(0);
        brief.Omitted.ShouldNotContain(BriefSection.Daily);
    }

    [Fact]
    public async Task Brief_DailyFlag_NormalTagIsSilentWhereTheBlankTagOnTheSameStoreWarns()
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "medivh opened the board", Utc(2026, 11, 9, 5), "medivh", "claude");

        var configured = await ServiceAt(MoscowMondayUtc, store, DailyOptions())
            .BriefAsync(Query(daily: true), CancellationToken.None);
        var blank = await ServiceAt(MoscowMondayUtc, store, DailyOptions(tag: ""))
            .BriefAsync(Query(daily: true), CancellationToken.None);

        configured.Warnings.ShouldBeNull();
        configured.DailyState.ShouldBe(BriefSectionState.Populated);
        blank.Warnings.ShouldHaveSingleItem();
        blank.DailyState.ShouldBe(BriefSectionState.Empty);
    }

    [Fact]
    public async Task Brief_DailyFlag_BlankTagAndUnresolvableZone_WarnTagFirstThenZone()
    {
        var brief = await ServiceAt(MoscowMondayUtc, new FakeMemoryStore(), DailyOptions(zone: "Not/AZone", tag: ""))
            .BriefAsync(Query(daily: true), CancellationToken.None);

        var warnings = brief.Warnings!.ToArray();
        warnings.Length.ShouldBe(2);
        warnings[0].ShouldBe("DailyTag is empty; the daily section is disabled. Set DailyTag to enable it.");
        warnings[1].ShouldBe("DailyTimeZone 'Not/AZone' did not resolve; daily buckets use UTC.");
    }

    [Fact]
    public async Task Brief_FlagOff_BlankTagAnnouncesNothingAboutASectionNobodyAskedFor()
    {
        SeedSnapshotFixture(_store, _manifests);

        var windowReads = 0;
        _store.OnListAsync = filter =>
        {
            if (filter.CreatedAfter is not null)
            {
                windowReads++;
            }
        };

        var brief = await Service(new MemoryEngineOptions { DailyTimeZone = "Europe/Moscow", DailyTag = "" })
            .BriefAsync(Query(), CancellationToken.None);

        _store.Records.Count(record => record.Tags.Contains("daily-notes")).ShouldBe(1);
        windowReads.ShouldBe(0);
        PayloadMatchesSnapshot(brief, "2d86019df2275119a42f01e2e8b31c8bac877d34b1b5dd5137d8a4812873628d");
    }

    [Fact]
    public async Task Brief_FlagOff_PayloadMatchesThePreFeatureSnapshot()
    {
        SeedSnapshotFixture(_store, _manifests);

        var brief = await Service(new MemoryEngineOptions { DailyTimeZone = "Europe/Moscow" })
            .BriefAsync(Query(), CancellationToken.None);

        PayloadMatchesSnapshot(brief, "2d86019df2275119a42f01e2e8b31c8bac877d34b1b5dd5137d8a4812873628d");
    }

    [Fact]
    public async Task Brief_FlagOff_WithOmittedSections_MatchesTheDigestBearingSnapshot()
    {
        SeedSnapshotFixture(_store, _manifests);

        var brief = await Service(new MemoryEngineOptions { BriefMaxTotalChars = 120, DailyTimeZone = "Europe/Moscow" })
            .BriefAsync(Query(), CancellationToken.None);

        brief.Omitted.ShouldNotBeEmpty();
        // re-pinned 2026-11-13 on the T2 digest slice (base carrier 6b78fa3): the fixture is
        // SeedSnapshotFixture with BriefMaxTotalChars = 120, whose journal section falls out of budget,
        // so this payload now legitimately carries omissionDigest{Journal: 2 SectionBudget lines}.
        // The method lost "PreFeatureSnapshot" from its name at the same time — the payload it pins is
        // no longer the pre-feature one. The three no-omission pin sites (2d86019d…) did NOT move.
        PayloadMatchesSnapshot(brief, "b31d8c8b9550aaf8a7658bbc66f51bf0c19b6eeadff468a961c57f050179190a");
    }

    [Fact]
    public async Task Brief_Window_FactExactlyOnUntilIsOutAndOneTickEarlierIsIn()
    {
        var until = FakeClock.DefaultStart;
        Seed("fact in by one tick", kind: MemoryKind.Fact, importance: 1, created: until.AddTicks(-1));
        Seed("fact exactly on until", kind: MemoryKind.Fact, importance: 1, created: until);

        var brief = await Service().BriefAsync(
            Query(since: until.AddDays(-1), until: until), CancellationToken.None);

        brief.Facts.Select(fact => fact.Content).ShouldBe(["fact in by one tick"]);
    }

    [Fact]
    public async Task Brief_Window_SinceBoundIsInclusiveAndOneTickEarlierIsOut()
    {
        var since = FakeClock.DefaultStart.AddDays(-2);
        Seed("fact exactly on since", kind: MemoryKind.Fact, importance: 1, created: since);
        Seed("fact one tick before since", kind: MemoryKind.Fact, importance: 1, created: since.AddTicks(-1));

        var brief = await Service().BriefAsync(Query(since: since), CancellationToken.None);

        brief.Facts.Select(fact => fact.Content).ShouldBe(["fact exactly on since"]);
    }

    [Fact]
    public async Task Brief_Window_JournalExactlyOnUntilIsOutAndOneTickEarlierIsIn()
    {
        var until = FakeClock.DefaultStart;
        Seed("journal in by one tick", created: until.AddTicks(-1));
        Seed("journal exactly on until", created: until);

        var brief = await Service().BriefAsync(
            Query(since: until.AddDays(-1), until: until), CancellationToken.None);

        brief.Journal.Select(entry => entry.Content).ShouldBe(["journal in by one tick"]);
    }

    [Fact]
    public async Task Brief_Window_PassesTheBoundsToTheFactsAndJournalReads()
    {
        var captured = new List<MemoryFilter>();
        _store.OnListAsync = captured.Add;
        var since = FakeClock.DefaultStart.AddDays(-1);
        var until = FakeClock.DefaultStart;

        await Service().BriefAsync(Query(since: since, until: until), CancellationToken.None);

        captured.Count.ShouldBeGreaterThan(0);
        captured.Count(filter => filter.Kind == MemoryKind.Fact
            && filter.CreatedAfter == since && filter.CreatedBefore == until).ShouldBe(1);

        // the untagged journal reads are the journal section and the compaction backlog, and only the
        // section is bounded by the caller's window, hence the exact count rather than a blanket rule
        captured.Count(filter => filter.Kind == MemoryKind.Journal && filter.Tags.Count == 0
            && filter.CreatedAfter == since && filter.CreatedBefore == until).ShouldBe(1);
    }

    [Fact]
    public async Task Brief_Window_DailyNoteExactlyOnUntilIsOutAndOneTickEarlierIsIn()
    {
        var store = new FakeMemoryStore();
        var until = Utc(2026, 11, 9, 6);
        SeedDailyNote(store, "daily in by one tick", until.AddTicks(-1), "medivh", "claude");
        SeedDailyNote(store, "daily exactly on until", until, "medivh", "claude");

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions())
            .BriefAsync(Query(daily: true, since: Utc(2026, 11, 9, 0), until: until), CancellationToken.None);

        brief.DailyGroups.ShouldHaveSingleItem();
        brief.DailyGroups[0].Bucket.ShouldBe(BriefDailyBucket.Today);
        brief.DailyGroups[0].Entries.Select(entry => entry.Content).ToList().ShouldBe(["daily in by one tick"]);
        brief.DailyMore.ShouldBe(0);
    }

    [Fact]
    public async Task Brief_Window_DailyNoteOutsideTheWindowIsNotCountedInMore()
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "in window note", Utc(2026, 11, 9, 5), "medivh", "claude");
        SeedDailyNote(store, "after the until bound", Utc(2026, 11, 9, 7), "medivh", "claude");

        var bounded = await ServiceAt(MoscowMondayUtc, store, DailyOptions(totalCap: 14))
            .BriefAsync(Query(daily: true, until: Utc(2026, 11, 9, 6)), CancellationToken.None);
        var open = await ServiceAt(MoscowMondayUtc, store, DailyOptions(totalCap: 14))
            .BriefAsync(Query(daily: true), CancellationToken.None);

        bounded.DailyGroups.SelectMany(group => group.Entries).Count().ShouldBe(1);
        bounded.DailyMore.ShouldBe(0);
        bounded.Omitted.ShouldNotContain(BriefSection.Daily);

        // the same store and the same purse with no window is what pushes the second note into more;
        // without this control the assertion above could be green because nothing ever overflowed
        open.DailyGroups.SelectMany(group => group.Entries).Count().ShouldBe(1);
        open.DailyMore.ShouldBe(1);
    }

    [Fact]
    public async Task Brief_Window_IntersectsTheDailyBucketsAndSkipsTheReadsItEmpties()
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "in window note", Utc(2026, 11, 9, 5), "medivh", "claude");
        SeedDailyNote(store, "friday note", Utc(2026, 11, 6, 8), "kelthuzad", "claude");

        var captured = new List<MemoryFilter>();
        store.OnListAsync = captured.Add;
        var since = Utc(2026, 11, 9, 0);
        var until = Utc(2026, 11, 9, 6);

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions())
            .BriefAsync(Query(daily: true, since: since, until: until), CancellationToken.None);

        var bucketReads = captured.Where(filter => filter.Tags.Contains("daily-notes")).ToArray();
        bucketReads.ShouldHaveSingleItem();
        bucketReads[0].CreatedAfter.ShouldBe(since);
        bucketReads[0].CreatedBefore.ShouldBe(until);
        brief.DailyGroups.SelectMany(group => group.Entries).Count().ShouldBe(1);
    }

    [Fact]
    public async Task Brief_WindowUnset_NoReadCarriesDateBoundsAndThePayloadIsThePinnedOne()
    {
        SeedSnapshotFixture(_store, _manifests);

        var captured = new List<MemoryFilter>();
        _store.OnListAsync = captured.Add;

        var brief = await Service(new MemoryEngineOptions { DailyTimeZone = "Europe/Moscow" })
            .BriefAsync(Query(), CancellationToken.None);

        captured.Count.ShouldBeGreaterThan(0);
        captured.ShouldAllBe(filter => filter.CreatedAfter == null && filter.CreatedBefore == null);
        PayloadMatchesSnapshot(brief, "2d86019df2275119a42f01e2e8b31c8bac877d34b1b5dd5137d8a4812873628d");
    }

    [Fact]
    public async Task CollectFacts_PerScopeTrimHandsBackTheDroppedRemainder()
    {
        var one = Seed("fact one", kind: MemoryKind.Fact, importance: 1);
        var two = Seed("fact two", kind: MemoryKind.Fact, importance: 1, created: FakeClock.DefaultStart.AddDays(-1));
        var three = Seed("fact three", kind: MemoryKind.Fact, importance: 1, created: FakeClock.DefaultStart.AddDays(-2));
        var four = Seed("fact four", kind: MemoryKind.Fact, importance: 1, created: FakeClock.DefaultStart.AddDays(-3));
        var global = Seed("global fact", kind: MemoryKind.Fact, scope: MemoryScope.Global, importance: 1);

        var (kept, dropped) = await Service().CollectFactsAsync(
            Query(factsPerScope: 2),
            [MemoryScope.Project, MemoryScope.Global],
            TomeKey.DefaultTome,
            FakeClock.DefaultStart,
            CancellationToken.None);

        // one and global tie at score 1.0 and the final fact order breaks the tie by Id,
        // so the kept list pins the set; the dropped tail lives inside one scope group and is ordered
        kept.Select(fact => fact.Id).OrderBy(id => id).ShouldBe(new[] { one.Id, global.Id, two.Id }.OrderBy(id => id));
        dropped.Select(record => record.Id).ToList().ShouldBe([three.Id, four.Id]);
        dropped.Select(record => record.Content).ToList().ShouldBe(["fact three", "fact four"]);
        kept.Select(fact => fact.Id).ToHashSet().Overlaps(dropped.Select(record => record.Id).ToHashSet()).ShouldBeFalse();
    }

    [Fact]
    public async Task CollectTombstones_LimitCutHandsBackTheRemainderInCutOrder()
    {
        var fresh = Seed("fresh decision", status: MemoryStatus.Tombstoned, tombstoneReason: "wrong fact", tombstonedAt: FakeClock.DefaultStart.AddDays(-1));
        var middle = Seed("middle decision", status: MemoryStatus.Tombstoned, tombstoneReason: "retracted", tombstonedAt: FakeClock.DefaultStart.AddDays(-2));
        var stale = Seed("stale decision", status: MemoryStatus.Tombstoned, tombstoneReason: "superseded by vote", tombstonedAt: FakeClock.DefaultStart.AddDays(-5));

        var (kept, dropped) = await Service().CollectTombstonesAsync(
            Query(tombstoneLimit: 2),
            [MemoryScope.Project, MemoryScope.Global],
            TomeKey.DefaultTome,
            CancellationToken.None);

        kept.Select(tombstone => tombstone.Id).ToList().ShouldBe([fresh.Id, middle.Id]);
        dropped.Select(record => record.Id).ToList().ShouldBe([stale.Id]);
        dropped[0].TombstoneReason.ShouldBe("superseded by vote");
    }

    [Fact]
    public async Task CollectJournal_PerScopeTrimHandsBackTheDroppedRemainder()
    {
        var one = Seed("journal one", created: FakeClock.DefaultStart);
        var two = Seed("journal two", created: FakeClock.DefaultStart.AddHours(-1));
        var three = Seed("journal three", created: FakeClock.DefaultStart.AddHours(-2));
        var global = Seed("global journal", scope: MemoryScope.Global, created: FakeClock.DefaultStart.AddHours(-3));

        var (kept, dropped) = await Service().CollectJournalAsync(
            Query(journalPerScope: 2),
            [MemoryScope.Project, MemoryScope.Global],
            TomeKey.DefaultTome,
            null,
            CancellationToken.None);

        kept.Select(entry => entry.Id).ToList().ShouldBe([one.Id, two.Id, global.Id]);
        dropped.Select(record => record.Id).ToList().ShouldBe([three.Id]);
        dropped[0].Content.ShouldBe("journal three");
    }

    [Fact]
    public async Task CollectJournal_DailyExcludedIdsAreNotHandedBackAsDropped()
    {
        var excluded = Seed("journal excluded by daily", created: FakeClock.DefaultStart);
        var one = Seed("journal one", created: FakeClock.DefaultStart.AddHours(-1));
        var two = Seed("journal two", created: FakeClock.DefaultStart.AddHours(-2));
        var three = Seed("journal three", created: FakeClock.DefaultStart.AddHours(-3));

        var (kept, dropped) = await Service().CollectJournalAsync(
            Query(journalPerScope: 2),
            [MemoryScope.Project, MemoryScope.Global],
            TomeKey.DefaultTome,
            new HashSet<Guid>([excluded.Id]),
            CancellationToken.None);

        kept.Select(entry => entry.Id).ToList().ShouldBe([one.Id, two.Id]);
        dropped.Select(record => record.Id).ToList().ShouldBe([three.Id]);
        dropped.Select(record => record.Id).ShouldNotContain(excluded.Id);
    }

    [Fact]
    public async Task CollectDaily_PurseTailHandsBackTheUnwrittenRecords()
    {
        var store = new FakeMemoryStore();
        var notes = new List<MemoryRecord>();
        for (var index = 0; index < 4; index++)
        {
            notes.Add(SeedDailyNote(store, new string((char)('a' + index), 40), Utc(2026, 11, 9, 3 + index), "medivh", "claude"));
        }

        var (groups, ids, more, _, _, dropped) = await ServiceAt(MoscowMondayUtc, store, DailyOptions(totalCap: 100))
            .CollectDailyAsync(
                DailyWindowResolver.ResolveZone("Europe/Moscow"),
                [MemoryScope.Project, MemoryScope.Global],
                TomeKey.DefaultTome,
                Query(daily: true),
                CancellationToken.None);

        groups.SelectMany(group => group.Entries).Select(entry => entry.Content).ToList()
            .ShouldBe([new string('a', 40), new string('b', 40)]);
        more.ShouldBe(2);
        dropped.Select(record => record.Id).ToList().ShouldBe([notes[2].Id, notes[3].Id]);
        // ids is computed upstream of the kept/dropped split, so this counts what the split alone cannot:
        // nothing was both written and dropped
        ids.Count.ShouldBe(groups.SelectMany(group => group.Entries).Count() + dropped.Count);
        dropped.Select(record => record.Content).ToList().ShouldBe([new string('c', 40), new string('d', 40)]);
        ids.ShouldContain(notes[3].Id);
    }

    [Fact]
    public async Task Brief_Window_LandsOnAnEarlierCandidateWhenTheNewestOneHoldsNothingInsideTheWindow()
    {
        var store = new FakeMemoryStore();
        SeedDailyNote(store, "note outside the window", MoscowMidnightUtc(new DateOnly(2026, 11, 8)).AddHours(5), "medivh", "claude");
        SeedDailyNote(store, "note inside the window", MoscowMidnightUtc(new DateOnly(2026, 11, 7)).AddHours(5), "kelthuzad", "claude");

        var bounded = await ServiceAt(MoscowMondayUtc, store, DailyOptions())
            .BriefAsync(Query(daily: true,
                since: MoscowMidnightUtc(new DateOnly(2026, 11, 7)),
                until: MoscowMidnightUtc(new DateOnly(2026, 11, 8))), CancellationToken.None);

        bounded.DailyGroups.ShouldHaveSingleItem();
        bounded.DailyGroups[0].Bucket.ShouldBe(BriefDailyBucket.LastWorkingDay);
        bounded.DailyGroups[0].Day.ShouldBe(new DateOnly(2026, 11, 7));
        bounded.DailyGroups[0].Entries.Select(entry => entry.Content).ToList().ShouldBe(["note inside the window"]);

        // the same store without the window lands on the newest holding candidate, so the
        // assertion above measures the window moving the loop and not an unrelated fixture quirk
        var open = await ServiceAt(MoscowMondayUtc, store, DailyOptions())
            .BriefAsync(Query(daily: true), CancellationToken.None);

        open.DailyGroups.ShouldHaveSingleItem();
        open.DailyGroups[0].Bucket.ShouldBe(BriefDailyBucket.LastWorkingDay);
        open.DailyGroups[0].Day.ShouldBe(new DateOnly(2026, 11, 8));
        open.DailyGroups[0].Entries.Select(entry => entry.Content).ToList().ShouldBe(["note outside the window"]);
    }

    [Fact]
    public async Task Brief_OmissionDigest_PerScopeTrimNamesEveryDroppedFact()
    {
        SeedIn(_store, "fact fresh", kind: MemoryKind.Fact, importance: 1, id: StableId(1));
        var mid = SeedIn(_store, "fact mid", kind: MemoryKind.Fact, importance: 1,
            created: FakeClock.DefaultStart.AddDays(-1), id: StableId(2));
        var old = SeedIn(_store, "fact old", kind: MemoryKind.Fact, importance: 1,
            created: FakeClock.DefaultStart.AddDays(-2), id: StableId(3));

        var brief = await Service().BriefAsync(Query(factsPerScope: 1), CancellationToken.None);

        brief.Facts.Select(fact => fact.Content).ShouldBe(["fact fresh"]);
        var section = brief.OmissionDigest![BriefSection.Facts];
        section.Count.ShouldBe(2);
        section.Dropped.Select(line => line.MemoryId).ToList().ShouldBe([mid.Id, old.Id]);
        section.Dropped.Select(line => line.Excerpt).ToList().ShouldBe(["fact mid", "fact old"]);
        section.Dropped.ShouldAllBe(line => line.Reason == BriefOmissionReason.ReadLimit);
        section.Count.ShouldBe(section.Dropped.Count);
    }

    [Fact]
    public async Task Brief_OmissionDigest_TombstoneCutAtLimitIsNamedPerRecord()
    {
        SeedIn(_store, "buried fresh", kind: MemoryKind.Journal, status: MemoryStatus.Tombstoned,
            tombstoneReason: "wrong fact", tombstonedAt: FakeClock.DefaultStart, id: StableId(1));
        var middle = SeedIn(_store, "buried middle", kind: MemoryKind.Journal, status: MemoryStatus.Tombstoned,
            tombstoneReason: "retracted mid", tombstonedAt: FakeClock.DefaultStart.AddDays(-1), id: StableId(2));
        var oldest = SeedIn(_store, "buried oldest", kind: MemoryKind.Journal, status: MemoryStatus.Tombstoned,
            tombstoneReason: "superseded by vote", tombstonedAt: FakeClock.DefaultStart.AddDays(-2), id: StableId(3));

        var brief = await Service().BriefAsync(Query(tombstoneLimit: 1), CancellationToken.None);

        brief.Tombstones.ShouldHaveSingleItem();
        var section = brief.OmissionDigest![BriefSection.Tombstones];
        section.Count.ShouldBe(2);
        section.Dropped.Select(line => line.MemoryId).ToList().ShouldBe([middle.Id, oldest.Id]);
        // the excerpt of a tombstone is the reason it was buried — that is what the section would have shown
        section.Dropped.Select(line => line.Excerpt).ToList().ShouldBe(["retracted mid", "superseded by vote"]);
        section.Dropped.ShouldAllBe(line => line.Reason == BriefOmissionReason.ReadLimit);
    }

    [Fact]
    public async Task Brief_OmissionDigest_SectionOutOfBudgetListsItsOwnRecordsBesideTheTrimmedTail()
    {
        SeedIn(_store, new string('f', 40), kind: MemoryKind.Fact, importance: 1, id: StableId(1));
        SeedIn(_store, new string('e', 40), kind: MemoryKind.Fact, importance: 1,
            created: FakeClock.DefaultStart.AddDays(-1), id: StableId(2));
        var trimmed = SeedIn(_store, new string('d', 40), kind: MemoryKind.Fact, importance: 1,
            created: FakeClock.DefaultStart.AddDays(-2), id: StableId(3));

        var brief = await Service(new MemoryEngineOptions { BriefMaxTotalChars = 60 })
            .BriefAsync(Query(factsPerScope: 2), CancellationToken.None);

        // one 40-char fact fits the 60-char purse; the second goes to the budget tail, the third was
        // already outside the factsPerScope read limit
        brief.FactsState.ShouldBe(BriefSectionState.Populated);
        brief.Facts.Select(fact => fact.Id).ShouldBe([StableId(1)]);
        brief.Omitted.ShouldBe([]);

        var section = brief.OmissionDigest![BriefSection.Facts];
        section.Count.ShouldBe(2);
        section.Dropped.Select(line => line.Reason).ToList().ShouldBe([
            BriefOmissionReason.SectionBudget,
            BriefOmissionReason.ReadLimit,
        ]);
        section.Dropped[^1].MemoryId.ShouldBe(trimmed.Id);
    }

    [Fact]
    public async Task Brief_OmissionDigest_DailyPurseTailListsTheUnwrittenNotes()
    {
        var store = new FakeMemoryStore();
        for (var index = 0; index < 4; index++)
        {
            SeedDailyNote(store, new string((char)('a' + index), 40), Utc(2026, 11, 9, 3 + index), "medivh", "claude");
        }

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions(totalCap: 100))
            .BriefAsync(Query(daily: true), CancellationToken.None);

        var section = brief.OmissionDigest![BriefSection.Daily];
        section.Count.ShouldBe(brief.DailyMore);
        section.Count.ShouldBe(2);
        section.Dropped.Select(line => line.Excerpt).ToList().ShouldBe([new string('c', 40), new string('d', 40)]);
        section.Dropped.ShouldAllBe(line => line.Reason == BriefOmissionReason.DailyPurse);
    }

    [Fact]
    public async Task Brief_OmissionDigest_ShortDroppedRecordStaysWholeAndLongOneIsCutWithItsId()
    {
        var shortText = new string('s', 250);
        var longText = new string('l', 500);
        SeedIn(_store, "the kept one", kind: MemoryKind.Fact, importance: 1, id: StableId(1));
        var whole = SeedIn(_store, shortText, kind: MemoryKind.Fact, importance: 1,
            created: FakeClock.DefaultStart.AddDays(-1), id: StableId(2));
        var folded = SeedIn(_store, "line one\nline  two\tline three", kind: MemoryKind.Fact, importance: 1,
            created: FakeClock.DefaultStart.AddDays(-2), id: StableId(3));
        var cut = SeedIn(_store, longText, kind: MemoryKind.Fact, importance: 1,
            created: FakeClock.DefaultStart.AddDays(-3), id: StableId(4));

        var brief = await Service().BriefAsync(Query(factsPerScope: 1), CancellationToken.None);

        var lines = brief.OmissionDigest![BriefSection.Facts].Dropped;
        lines.Select(line => line.MemoryId).ToList().ShouldBe([whole.Id, folded.Id, cut.Id]);

        lines[0].Excerpt.ShouldBe(shortText);
        lines[0].ExcerptCut.ShouldBeFalse();

        lines[1].Excerpt.ShouldBe("line one line two line three");
        lines[1].ExcerptCut.ShouldBeFalse();

        lines[2].Excerpt.ShouldBe(longText[..MemoryEngineOptions.DefaultBriefDigestExcerptChars]);
        lines[2].Excerpt.Length.ShouldBe(80);
        lines[2].ExcerptCut.ShouldBeTrue();
        lines[2].MemoryId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Brief_OmissionDigest_ThresholdsAreNamedDefaultsNotMeasuredNumbers()
    {
        MemoryEngineOptions.DefaultBriefDigestExcerptChars.ShouldBe(80);
        MemoryEngineOptions.DefaultBriefNeverTruncateChars.ShouldBe(300);

        var options = new MemoryEngineOptions();
        options.BriefDigestExcerptChars.ShouldBe(MemoryEngineOptions.DefaultBriefDigestExcerptChars);
        options.BriefNeverTruncateChars.ShouldBe(MemoryEngineOptions.DefaultBriefNeverTruncateChars);
    }

    [Fact]
    public async Task Brief_JournalCap_NeverTruncatesAShortRecordEvenUnderATightCap()
    {
        var shortNote = new string('n', 120);
        SeedIn(_store, shortNote, created: FakeClock.DefaultStart);
        SeedIn(_store, new string('m', 500), created: FakeClock.DefaultStart.AddHours(-1));

        var brief = await Service(new MemoryEngineOptions { BriefMaxEntryChars = 50 })
            .BriefAsync(Query(), CancellationToken.None);

        brief.Journal[0].Content.ShouldBe(shortNote);
        brief.Journal[0].Truncated.ShouldBeFalse();

        // the cap still applies above the floor, otherwise the guarantee would be a silent cap override
        brief.Journal[1].Content.Length.ShouldBe(50);
        brief.Journal[1].Truncated.ShouldBeTrue();
    }

    [Fact]
    public async Task Brief_DailyFlag_NeverTruncatesAShortNoteEvenUnderATightEntryCap()
    {
        var store = new FakeMemoryStore();
        var shortNote = new string('n', 200);
        SeedDailyNote(store, shortNote, Utc(2026, 11, 9, 5), "medivh", "claude");

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions(entryCap: 40))
            .BriefAsync(Query(daily: true), CancellationToken.None);

        var entry = brief.DailyGroups.SelectMany(group => group.Entries).Single();
        entry.Content.ShouldBe(shortNote);
        entry.Truncated.ShouldBeFalse();
    }

    [Fact]
    public async Task Brief_OmissionDigest_NoOmissions_SerializesNoDigestKeyAtAll()
    {
        SeedSnapshotFixture(_store, _manifests);

        var brief = await Service(new MemoryEngineOptions { DailyTimeZone = "Europe/Moscow" })
            .BriefAsync(Query(), CancellationToken.None);

        brief.OmissionDigest.ShouldBeNull();
        CanonicalPayload(brief).ShouldNotContain("OmissionDigest");
    }

    [Fact]
    public async Task Brief_OmissionDigest_WithOmissions_SerializesCountAndPerRecordLines()
    {
        SeedIn(_store, "fact fresh", kind: MemoryKind.Fact, importance: 1, id: StableId(1));
        SeedIn(_store, "fact trimmed", kind: MemoryKind.Fact, importance: 1,
            created: FakeClock.DefaultStart.AddDays(-1), id: StableId(2));

        var brief = await Service().BriefAsync(Query(factsPerScope: 1), CancellationToken.None);

        var payload = CanonicalPayload(brief);
        payload.ShouldContain("OmissionDigest");
        payload.ShouldContain("Fact");
        payload.ShouldContain("fact trimmed");
        payload.ShouldContain("ReadLimit");
    }

    [Fact]
    public async Task Brief_OmissionDigest_FactsOrderOnDeclaredKeysNotOnTheOrderTheReadCameIn()
    {
        var globalTop = SeedIn(_store, "global top", scope: MemoryScope.Global, kind: MemoryKind.Fact,
            importance: 1, id: StableId(4));
        var projectOldest = SeedIn(_store, "project oldest", kind: MemoryKind.Fact, importance: 1,
            created: FakeClock.DefaultStart.AddDays(-4), id: StableId(3));
        var globalTrimmed = SeedIn(_store, "global trimmed", scope: MemoryScope.Global, kind: MemoryKind.Fact,
            importance: 1, created: FakeClock.DefaultStart.AddDays(-2), id: StableId(1));
        var projectMid = SeedIn(_store, "project mid", kind: MemoryKind.Fact, importance: 1,
            created: FakeClock.DefaultStart.AddDays(-3), id: StableId(2));
        var projectTop = SeedIn(_store, "project top", kind: MemoryKind.Fact, importance: 1,
            created: FakeClock.DefaultStart.AddDays(-1), id: StableId(5));

        var brief = await Service().BriefAsync(
            Query(factsPerScope: 1, scopes: [MemoryScope.Project, MemoryScope.Global]), CancellationToken.None);

        brief.Facts.Select(fact => fact.Id).OrderBy(id => id)
            .ShouldBe(new[] { globalTop.Id, projectTop.Id }.OrderBy(id => id));

        // seed order put the global trimmed record first and the collection hands its tail back in
        // group-creation order; the declared contract is the requested scope list, so project first
        brief.OmissionDigest![BriefSection.Facts].Dropped.Select(line => line.MemoryId).ToList()
            .ShouldBe([projectMid.Id, projectOldest.Id, globalTrimmed.Id]);
    }

    [Fact]
    public async Task Brief_OmissionDigest_TombstonesOrderNewestFirstWithIdBreakingTheTie()
    {
        var tieHigh = SeedIn(_store, "buried high", kind: MemoryKind.Journal, status: MemoryStatus.Tombstoned,
            tombstoneReason: "high id tie", tombstonedAt: FakeClock.DefaultStart.AddDays(-5), id: StableId(9));
        var later = SeedIn(_store, "buried later", kind: MemoryKind.Journal, status: MemoryStatus.Tombstoned,
            tombstoneReason: "later cut", tombstonedAt: FakeClock.DefaultStart.AddDays(-4), id: StableId(3));
        var tieLow = SeedIn(_store, "buried low", kind: MemoryKind.Journal, status: MemoryStatus.Tombstoned,
            tombstoneReason: "low id tie", tombstonedAt: FakeClock.DefaultStart.AddDays(-5), id: StableId(2));
        SeedIn(_store, "buried fresh", kind: MemoryKind.Journal, status: MemoryStatus.Tombstoned,
            tombstoneReason: "wrong fact", tombstonedAt: FakeClock.DefaultStart, id: StableId(1));

        var brief = await Service().BriefAsync(Query(tombstoneLimit: 1), CancellationToken.None);

        // seeded oldest-of-the-tail first and the highest id before the lowest one: neither the seed
        // order nor a plain id sort produces this list, only the declared tombstoned-at then id keys
        brief.OmissionDigest![BriefSection.Tombstones].Dropped.Select(line => line.MemoryId).ToList()
            .ShouldBe([later.Id, tieLow.Id, tieHigh.Id]);
    }

    [Fact]
    public async Task Brief_OmissionDigest_JournalOrdersOnDeclaredKeysNotOnTheOrderTheReadCameIn()
    {
        var globalTop = SeedIn(_store, "journal global top", scope: MemoryScope.Global,
            created: FakeClock.DefaultStart, id: StableId(4));
        var projectOldest = SeedIn(_store, "journal project oldest",
            created: FakeClock.DefaultStart.AddDays(-4), id: StableId(3));
        var globalTrimmed = SeedIn(_store, "journal global trimmed", scope: MemoryScope.Global,
            created: FakeClock.DefaultStart.AddDays(-2), id: StableId(1));
        var projectMid = SeedIn(_store, "journal project mid",
            created: FakeClock.DefaultStart.AddDays(-3), id: StableId(2));
        var projectTop = SeedIn(_store, "journal project top",
            created: FakeClock.DefaultStart.AddDays(-1), id: StableId(5));

        var brief = await Service().BriefAsync(
            Query(journalPerScope: 1, scopes: [MemoryScope.Project, MemoryScope.Global]), CancellationToken.None);

        brief.Journal.Select(entry => entry.Id).OrderBy(id => id)
            .ShouldBe(new[] { globalTop.Id, projectTop.Id }.OrderBy(id => id));

        brief.OmissionDigest![BriefSection.Journal].Dropped.Select(line => line.MemoryId).ToList()
            .ShouldBe([projectMid.Id, projectOldest.Id, globalTrimmed.Id]);
    }

    [Fact]
    public async Task Brief_OmissionDigest_DailyOrdersDayThenActorThenTimeNotTheSeedOrder()
    {
        var store = new FakeMemoryStore();
        var todayZoe = SeedDailyNote(store, new string('a', 40), Utc(2026, 11, 9, 5), "zoe", "claude");
        var fridayAbe = SeedDailyNote(store, new string('b', 40),
            MoscowMidnightUtc(new DateOnly(2026, 11, 6)).AddHours(9), "abe", "claude");
        var todayAbe = SeedDailyNote(store, new string('c', 40), Utc(2026, 11, 9, 4), "abe", "claude");
        var fridayZoe = SeedDailyNote(store, new string('d', 40),
            MoscowMidnightUtc(new DateOnly(2026, 11, 6)).AddHours(10), "ZOE", "claude");

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions(totalCap: 40))
            .BriefAsync(Query(daily: true), CancellationToken.None);

        brief.DailyGroups.SelectMany(group => group.Entries).Select(entry => entry.Id).ShouldBe([fridayAbe.Id]);

        // the seed order is the exact reverse of the contract, so a digest that inherits enumeration
        // order or sorts on one key emits a different list and goes red here
        brief.OmissionDigest![BriefSection.Daily].Dropped.Select(line => line.MemoryId).ToList()
            .ShouldBe([fridayZoe.Id, todayAbe.Id, todayZoe.Id]);
    }

    [Fact]
    public async Task Brief_OmissionDigest_RecordOutsideTheWindowAppearsInNeitherJournalNorDigest()
    {
        var until = FakeClock.DefaultStart;
        SeedIn(_store, "in window fresh", kind: MemoryKind.Fact, importance: 1, created: until.AddTicks(-1),
            id: StableId(1));
        var trimmed = SeedIn(_store, "in window trimmed", kind: MemoryKind.Fact, importance: 1,
            created: FakeClock.DefaultStart.AddDays(-1), id: StableId(2));
        var outside = SeedIn(_store, "before the since bound", kind: MemoryKind.Fact, importance: 1,
            created: FakeClock.DefaultStart.AddDays(-10), id: StableId(3));

        var bounded = await Service().BriefAsync(
            Query(factsPerScope: 1, since: until.AddDays(-3), until: until), CancellationToken.None);

        var lines = bounded.OmissionDigest![BriefSection.Facts].Dropped;
        lines.Select(line => line.MemoryId).ToList().ShouldBe([trimmed.Id]);
        bounded.Facts.Select(fact => fact.Id).ShouldNotContain(outside.Id);
        lines.ShouldNotContain(line => line.MemoryId == outside.Id);

        // control: the same store with no window does lose that record to the trim, so the assertion
        // above measures the exclusion and not a fixture that never had a third fact in the tail
        var open = await Service().BriefAsync(Query(factsPerScope: 1), CancellationToken.None);
        open.OmissionDigest![BriefSection.Facts].Dropped
            .Select(line => line.MemoryId).ToList().ShouldBe([trimmed.Id, outside.Id]);
    }

    [Fact]
    public async Task Brief_OmissionDigest_NoteAlreadyShownAsDailyIsNotADigestLine()
    {
        var asDaily = SeedIn(_store, "daily note also journal", created: FakeClock.DefaultStart.AddHours(-3),
            tags: ["daily-notes"], id: StableId(1));
        var fresh = SeedIn(_store, "plain fresh note", created: FakeClock.DefaultStart.AddHours(-1), id: StableId(2));
        var trimmed = SeedIn(_store, "plain trimmed note", created: FakeClock.DefaultStart.AddHours(-2), id: StableId(3));

        var brief = await Service(DailyOptions()).BriefAsync(
            Query(daily: true, journalPerScope: 1), CancellationToken.None);

        brief.DailyGroups.SelectMany(group => group.Entries).Select(entry => entry.Id).ShouldBe([asDaily.Id]);
        var lines = brief.OmissionDigest![BriefSection.Journal].Dropped;
        lines.Select(line => line.MemoryId).ToList().ShouldBe([trimmed.Id]);
        lines.ShouldNotContain(line => line.MemoryId == asDaily.Id);
        brief.Journal.Select(entry => entry.Id).ShouldNotContain(asDaily.Id);

        // control: with the daily flag off the same record is in the journal running and lands in the
        // trimmed tail, so what keeps it out of the digest above is the exclusion, not the fixture
        var noDaily = await Service(DailyOptions()).BriefAsync(Query(journalPerScope: 1), CancellationToken.None);
        noDaily.OmissionDigest![BriefSection.Journal].Dropped
            .Select(line => line.MemoryId).ToList().ShouldBe([trimmed.Id, asDaily.Id]);
    }

    [Fact]
    public async Task Brief_OmissionDigest_BucketTheWindowEmptiesContributesNoDigestLines()
    {
        var store = new FakeMemoryStore();
        var friday = SeedDailyNote(store, new string('f', 40),
            MoscowMidnightUtc(new DateOnly(2026, 11, 6)).AddHours(9), "abe", "claude");
        SeedDailyNote(store, new string('a', 40), Utc(2026, 11, 9, 3), "abe", "claude");
        var todayMid = SeedDailyNote(store, new string('b', 40), Utc(2026, 11, 9, 4), "abe", "claude");
        var todayTail = SeedDailyNote(store, new string('c', 40), Utc(2026, 11, 9, 5), "abe", "claude");

        var brief = await ServiceAt(MoscowMondayUtc, store, DailyOptions(totalCap: 40))
            .BriefAsync(Query(daily: true, since: Utc(2026, 11, 9, 0), until: Utc(2026, 11, 10, 0)),
                CancellationToken.None);

        var section = brief.OmissionDigest![BriefSection.Daily];
        section.Count.ShouldBe(2);
        section.Dropped.Select(line => line.MemoryId).ToList().ShouldBe([todayMid.Id, todayTail.Id]);
        section.Dropped.ShouldNotContain(line => line.MemoryId == friday.Id);
    }

    private static readonly DateTimeOffset MoscowMondayUtc = new(2026, 11, 9, 7, 0, 0, TimeSpan.Zero);

    private static void PayloadMatchesSnapshot(BriefResult brief, string hash)
    {
        brief.Warnings.ShouldBeNull();

        var payload = CanonicalPayload(brief);
        payload.ShouldNotContain("\"Warnings\"");
        payload.Contains("OmissionDigest").ShouldBe(brief.OmissionDigest is not null);
        Sha256Hex(payload).ShouldBe(hash);
    }

    private static MemoryEngineOptions DailyOptions(
        string? zone = "Europe/Moscow",
        string tag = "daily-notes",
        int lookbackDays = 7,
        int entryCap = 1500,
        int totalCap = 6000) => new()
        {
            DailyTimeZone = zone,
            DailyTag = tag,
            DailyLookbackDays = lookbackDays,
            BriefDailyMaxEntryChars = entryCap,
            BriefDailyMaxTotalChars = totalCap,
        };

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private static DateTimeOffset MoscowMidnightUtc(DateOnly day) =>
        new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(3)).ToUniversalTime();

    private static DateOnly LocalDayOf(DateTimeOffset instant, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(instant, zone);
        return new DateOnly(local.Year, local.Month, local.Day);
    }

    private static MemoryRecord SeedDailyNote(
        FakeMemoryStore store,
        string content,
        DateTimeOffset created,
        string alias,
        string vendor) =>
        SeedIn(store, content, created: created, tags: ["daily-notes"], actor: MemoryActor.Agent(alias, vendor));

    private static void SeedSnapshotFixture(FakeMemoryStore store, FakeTomeManifestStore manifests)
    {
        SeedIn(
            store,
            "you are a grimoire keeper",
            scope: MemoryScope.Personal,
            kind: MemoryKind.Fact,
            tags: [PersonaSectionExtensions.PersonaTag, "persona:identity"],
            id: StableId(1));
        SeedIn(store, new string('f', 50), kind: MemoryKind.Fact, importance: 1, id: StableId(2));
        SeedIn(store, "global rule", kind: MemoryKind.Fact, scope: MemoryScope.Global, importance: 0.6, id: StableId(3));
        SeedIn(
            store,
            "buried decision",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "superseded by vote",
            tombstonedAt: FakeClock.DefaultStart.AddDays(-1),
            id: StableId(4));
        SeedIn(store, "plain journal note", created: FakeClock.DefaultStart.AddHours(-1), id: StableId(5));
        SeedIn(store, "daily heavy note", created: FakeClock.DefaultStart.AddMinutes(-30), tags: ["daily-notes"], id: StableId(6));
        manifests.Seed(new TomeManifest
        {
            Key = "alpha",
            Description = "the alpha tome",
            CreatedBy = "medivh",
            CreatedAt = FakeClock.DefaultStart.AddDays(-2),
            UpdatedAt = FakeClock.DefaultStart.AddDays(-2),
        });
    }

    private static Guid StableId(int slot) => Guid.Parse($"00000000-0000-4000-8000-0000000000{slot:x2}");

    private static string CanonicalPayload(BriefResult brief) =>
        JsonSerializer.Serialize(brief, SnapshotJson);

    private static string Sha256Hex(string payload) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

    private static readonly JsonSerializerOptions SnapshotJson = new()
    {
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private BriefService Service(MemoryEngineOptions? options = null) =>
        new(_store, new PersonaService(_store, _embeddings, _clock), _manifests, _clock, options);

    private BriefService ServiceAt(
        DateTimeOffset now,
        FakeMemoryStore store,
        MemoryEngineOptions options,
        FakeTomeManifestStore? manifests = null) =>
        new(store,
            new PersonaService(store, _embeddings, new FakeClock(now)),
            manifests ?? _manifests,
            new FakeClock(now),
            options);

    private static BriefQuery Query(
        string? tome = null,
        string? projectId = null,
        IReadOnlyList<MemoryScope>? scopes = null,
        int? factsPerScope = null,
        int? journalPerScope = null,
        int? tombstoneLimit = null,
        bool daily = false,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null) => new()
        {
            Tome = tome,
            ProjectId = projectId,
            Scopes = scopes ?? [],
            FactsPerScope = factsPerScope,
            JournalPerScope = journalPerScope,
            TombstoneLimit = tombstoneLimit,
            Daily = daily,
            Since = since,
            Until = until,
        };

    private MemoryRecord Seed(
        string content,
        MemoryScope scope = MemoryScope.Project,
        MemoryKind kind = MemoryKind.Journal,
        double importance = 0.5,
        DateTimeOffset? created = null,
        DateTimeOffset? accessed = null,
        string? projectId = null,
        string? sessionId = null,
        IReadOnlyList<string>? tags = null,
        MemoryStatus status = MemoryStatus.Active,
        Guid? supersededBy = null,
        string? tombstoneReason = null,
        DateTimeOffset? tombstonedAt = null,
        string? tome = null) =>
        SeedIn(_store, content, scope, kind, importance, created, accessed, projectId, sessionId, tags, status,
            supersededBy, tombstoneReason, tombstonedAt, tome);

    private static MemoryRecord SeedIn(
        FakeMemoryStore store,
        string content,
        MemoryScope scope = MemoryScope.Project,
        MemoryKind kind = MemoryKind.Journal,
        double importance = 0.5,
        DateTimeOffset? created = null,
        DateTimeOffset? accessed = null,
        string? projectId = null,
        string? sessionId = null,
        IReadOnlyList<string>? tags = null,
        MemoryStatus status = MemoryStatus.Active,
        Guid? supersededBy = null,
        string? tombstoneReason = null,
        DateTimeOffset? tombstonedAt = null,
        string? tome = null,
        MemoryActor? actor = null,
        Guid? id = null)
    {
        var record = TestMemories.Create(
            content,
            [],
            scope: scope,
            kind: kind,
            actor: actor,
            created: created,
            accessed: accessed,
            importance: importance,
            projectId: projectId,
            sessionId: sessionId,
            tags: tags,
            status: status,
            supersededBy: supersededBy,
            tombstoneReason: tombstoneReason,
            tombstonedAt: tombstonedAt,
            tome: tome);

        if (id is { } fixedId)
        {
            record = record with { Id = fixedId };
        }

        store.Seed(record, []);
        return record;
    }

    private MemoryRecord SeedStaleJournal(
        string content,
        float[] vector,
        MemoryScope scope = MemoryScope.Project,
        string? tome = null)
    {
        var aged = FakeClock.DefaultStart.AddDays(-30);
        var record = TestMemories.Create(
            content,
            [],
            scope: scope,
            kind: MemoryKind.Journal,
            created: aged,
            accessed: aged,
            projectId: "grimoire",
            tome: tome);
        _store.Seed(record, vector);
        return record;
    }

    private MemoryRecord SeedPersona(string content)
    {
        var record = TestMemories.Create(
            content,
            [],
            scope: MemoryScope.Personal,
            kind: MemoryKind.Fact,
            tags: [PersonaSectionExtensions.PersonaTag, "persona:identity"]);
        _store.Seed(record, []);
        return record;
    }
}
