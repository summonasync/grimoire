using Grimoire.Core.Tests.Fakes;
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
        brief.FactsState.ShouldBe(BriefSectionState.Omitted);
        brief.Facts.ShouldBeEmpty();
        brief.TombstonesState.ShouldBe(BriefSectionState.Populated);
        brief.JournalState.ShouldBe(BriefSectionState.Populated);
        brief.Omitted.ShouldBe([BriefSection.Facts]);

        var consumed = brief.Persona!.Identity.Length
            + brief.Tombstones.Sum(tombstone => tombstone.Reason!.Length)
            + brief.Journal.Sum(entry => entry.Content.Length);
        consumed.ShouldBeLessThanOrEqualTo(250);
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

    private BriefService Service(MemoryEngineOptions? options = null) =>
        new(_store, new PersonaService(_store, _embeddings, _clock), new FakeTomeManifestStore(), _clock, options);

    private static BriefQuery Query(
        string? tome = null,
        string? projectId = null,
        IReadOnlyList<MemoryScope>? scopes = null,
        int? factsPerScope = null,
        int? journalPerScope = null,
        int? tombstoneLimit = null) => new()
        {
            Tome = tome,
            ProjectId = projectId,
            Scopes = scopes ?? [],
            FactsPerScope = factsPerScope,
            JournalPerScope = journalPerScope,
            TombstoneLimit = tombstoneLimit,
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
        string? tome = null)
    {
        var record = TestMemories.Create(
            content,
            [],
            scope: scope,
            kind: kind,
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
        _store.Seed(record, []);
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
