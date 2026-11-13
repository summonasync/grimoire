using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryServiceReadFilterTests
{
    private const string QueryText = "roadmap";

    private static readonly IReadOnlyDictionary<string, float[]> Vectors = new Dictionary<string, float[]>
    {
        [QueryText] = [1f, 0f, 0f],
    };

    private readonly FakeMemoryStore _store = new();

    private readonly FakeClock _clock = new();

    private readonly MemoryService _service;

    public MemoryServiceReadFilterTests()
    {
        _service = TestMemories.Service(_store, new FakeEmbeddingProvider(Vectors), _clock);
    }

    [Fact]
    public async Task List_FilteredRecordSurvivesWhenLimitWouldCutIt()
    {
        Seed("wrong-actor", created: FakeClock.DefaultStart, actor: MemoryActor.Human);
        var wanted = Seed("in-window", created: FakeClock.DefaultStart);

        var page = await _service.ListAsync(new MemoryFilter
        {
            CreatedAfter = FakeClock.DefaultStart.AddDays(-1),
            Actor = "dotnet-coder",
            Limit = 1,
        }, CancellationToken.None);

        page.Count.ShouldBe(1);
        page[0].Id.ShouldBe(wanted.Id);
    }

    [Fact]
    public async Task List_AppliesFullConjunctionAcrossDimensions()
    {
        var moment = FakeClock.DefaultStart;
        var match = Seed("match", created: moment, tags: ["plan"]);
        Seed("early", created: moment.AddDays(-2), tags: ["plan"]);
        Seed("late", created: moment.AddDays(2), tags: ["plan"]);
        Seed("wrong-actor", created: moment, actor: MemoryActor.Human, tags: ["plan"]);
        Seed("wrong-scope", created: moment, scope: MemoryScope.Session, sessionId: "s-1", tags: ["plan"]);
        Seed("wrong-kind", created: moment, kind: MemoryKind.Fact, tags: ["plan"]);
        Seed("wrong-tags", created: moment, tags: ["other"]);

        var page = await _service.ListAsync(new MemoryFilter
        {
            Scopes = [MemoryScope.Project],
            Kind = MemoryKind.Journal,
            CreatedAfter = moment.AddDays(-1),
            CreatedBefore = moment.AddDays(1),
            Actor = "dotnet-coder",
            Tags = ["plan"],
        }, CancellationToken.None);

        page.Select(record => record.Id).ShouldBe([match.Id]);
    }

    [Fact]
    public async Task Recall_AppliesDateRangeAndActorBeforeRanking()
    {
        var inWindow = Seed("fresh note", [0.9f, 0.4359f, 0f], created: FakeClock.DefaultStart);
        Seed("stale note", [0.95f, 0.3122f, 0f], created: FakeClock.DefaultStart.AddDays(-60), actor: MemoryActor.Human);

        var matches = await _service.RecallAsync(Query(since: FakeClock.DefaultStart.AddDays(-1)), CancellationToken.None);

        matches.Count.ShouldBe(1);
        matches[0].Record.Id.ShouldBe(inWindow.Id);
    }

    [Fact]
    public async Task Recall_FilteredRecordDoesNotCrowdOutTheMatchingPage()
    {
        Seed("out of window", [0.99f, 0.141f, 0f], created: FakeClock.DefaultStart.AddDays(-90), actor: MemoryActor.Human);
        var wanted = Seed("in window", [0.8f, 0.6f, 0f], created: FakeClock.DefaultStart);

        var matches = await _service.RecallAsync(
            Query(limit: 1, until: FakeClock.DefaultStart.AddDays(1)), CancellationToken.None);

        matches.Count.ShouldBe(1);
        matches[0].Record.Id.ShouldBe(wanted.Id);
    }

    [Fact]
    public async Task Recall_ComposesDateAndActorWithTagsScopeAndKind()
    {
        var wanted = Seed("wanted", [0.9f, 0.4359f, 0f], created: FakeClock.DefaultStart, tags: ["plan"]);
        Seed("tagged out", [0.95f, 0.3122f, 0f], created: FakeClock.DefaultStart, tags: ["noise"]);
        Seed("actor out", [0.85f, 0.5268f, 0f], created: FakeClock.DefaultStart, tags: ["plan"], actor: MemoryActor.Human);

        var matches = await _service.RecallAsync(new RecallQuery
        {
            Text = QueryText,
            Tags = ["plan"],
            CreatedBefore = FakeClock.DefaultStart.AddHours(1),
            Actor = "dotnet-coder",
        }, CancellationToken.None);

        matches.Select(match => match.Record.Id).ShouldBe([wanted.Id]);
    }

    [Fact]
    public async Task Recall_WithAnActor_FillsThePageFromOneHonestWindow()
    {
        Seed("noise 1", [0.9950f, 0.0998f, 0f], actor: MemoryActor.Human);
        Seed("noise 2", [0.9899f, 0.1421f, 0f], actor: MemoryActor.Human);
        Seed("noise 3", [0.9848f, 0.1736f, 0f], actor: MemoryActor.Human);
        Seed("noise 4", [0.9798f, 0.2000f, 0f], actor: MemoryActor.Human);
        Seed("noise 5", [0.9747f, 0.2234f, 0f], actor: MemoryActor.Human);
        var first = Seed("deep 1", [0.8660f, 0.5f, 0f]);
        var second = Seed("deep 2", [0.4f, 0.9165f, 0f]);
        Seed("deep 3", [0.3f, 0.9539f, 0f]);

        // Was Recall_RefillsTheWindowWhenFiltersWashOutTheFirstPage: it proved the refill by seeding
        // a window full of another actor's rows, and a CLEAN actor candidate cannot do that any more.
        // Before/after on this identical fixture: two calls and twelve points fetched to hand back
        // two rows, now one call and six points. The loop itself is untouched and still reachable
        // through the "alias+" grammar - see the test that pins that, which falsified my earlier
        // claim that recall could no longer trigger it.
        var matches = await _service.RecallAsync(Query(limit: 2, actor: "dotnet-coder"), CancellationToken.None);

        _store.SearchCallCount.ShouldBe(1);
        matches.Select(match => match.Record.Id).ShouldBe([first.Id, second.Id]);
    }

    [Fact]
    public async Task Recall_WithATrailingSeparator_StaysCorrectOnlyBecauseThePostFilterStillRuns()
    {
        for (var index = 1; index <= 6; index++)
        {
            Seed($"mine {index}", [0.9f - index * 0.01f, 0.1f, 0f]);
        }

        // The pinned asymmetry with teeth, and the fixture is the point: with "alias+" the store
        // clause is alias-only, so it keeps this actor's own rows and drops everyone else's - which
        // is the opposite direction from what my first version of this test assumed. To fill the
        // window the rows have to be the actor's own: six of them, limit 2, oversampling 3, so the
        // 6-point window is full, the matcher then rejects all six for the trailing separator, and
        // the refill fires. For this one grammar the post-filter is the only thing standing between
        // this query and a wrong answer, which is why "recall can no longer trigger the escalation"
        // was false and why the asymmetry is on record rather than quietly aligned.
        var matches = await _service.RecallAsync(Query(limit: 2, actor: "dotnet-coder+"), CancellationToken.None);

        _store.SearchCallCount.ShouldBe(2);
        matches.ShouldBeEmpty();
    }

    [Fact]
    public async Task Recall_WhenEscalationIsStillMostlyNoise_ReturnsTheHonestShortPage()
    {
        Seed("noise 1", [0.9950f, 0.0998f, 0f], actor: MemoryActor.Human);
        Seed("noise 2", [0.9899f, 0.1421f, 0f], actor: MemoryActor.Human);
        Seed("noise 3", [0.9848f, 0.1736f, 0f], actor: MemoryActor.Human);
        Seed("noise 4", [0.9798f, 0.2000f, 0f], actor: MemoryActor.Human);
        Seed("noise 5", [0.9747f, 0.2234f, 0f], actor: MemoryActor.Human);
        var only = Seed("only honest", [0.8660f, 0.5f, 0f]);

        // The escalation loop itself is untouched by this slice (its own card), but its cause moved
        // for a CLEAN actor candidate: the store narrows the window to this actor's rows, one of them
        // survives, and the honest short page is reached in one call. Not a general statement — with
        // the trailing-separator grammar the window can still fill and be emptied afterwards, which
        // is what the trailing-separator test above exists to show.
        var matches = await _service.RecallAsync(Query(limit: 2, actor: "dotnet-coder"), CancellationToken.None);

        _store.SearchCallCount.ShouldBe(1);
        matches.Select(match => match.Record.Id).ShouldBe([only.Id]);
    }

    [Fact]
    public async Task Recall_DoesNotRefillWhenTheFirstWindowFillsThePage()
    {
        Seed("hot", [0.9f, 0.4359f, 0f]);
        Seed("warm", [0.8f, 0.6f, 0f]);
        Seed("mild", [0.7f, 0.7141f, 0f]);
        Seed("cool", [0.6f, 0.8f, 0f]);

        var matches = await _service.RecallAsync(Query(limit: 1), CancellationToken.None);

        _store.SearchCallCount.ShouldBe(1);
        matches.Count.ShouldBe(1);
        matches[0].Record.Content.ShouldBe("hot");
    }

    [Fact]
    public async Task Recall_DoesNotRefillWhenTheStoreIsAlreadyExhausted()
    {
        Seed("noise 1", [0.9950f, 0.0998f, 0f], actor: MemoryActor.Human);
        Seed("noise 2", [0.9899f, 0.1421f, 0f], actor: MemoryActor.Human);
        var only = Seed("only match", [0.8660f, 0.5f, 0f]);

        var matches = await _service.RecallAsync(Query(limit: 2, actor: "dotnet-coder"), CancellationToken.None);

        _store.SearchCallCount.ShouldBe(1);
        matches.Count.ShouldBe(1);
        matches[0].Record.Id.ShouldBe(only.Id);
    }

    private MemoryRecord Seed(
        string content,
        float[]? vector = null,
        DateTimeOffset? created = null,
        MemoryActor? actor = null,
        MemoryScope scope = MemoryScope.Project,
        MemoryKind kind = MemoryKind.Journal,
        string? sessionId = null,
        IReadOnlyList<string>? tags = null)
    {
        var embedded = vector ?? [1f, 0f, 0f];
        var record = TestMemories.Create(
            content,
            embedded,
            scope: scope,
            kind: kind,
            actor: actor,
            created: created,
            sessionId: sessionId,
            tags: tags);
        _store.Seed(record, embedded);
        return record;
    }

    private static RecallQuery Query(
        int limit = 5,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null,
        string? actor = null) => new()
        {
            Text = QueryText,
            Limit = limit,
            CreatedAfter = since,
            CreatedBefore = until,
            Actor = actor,
        };
}
