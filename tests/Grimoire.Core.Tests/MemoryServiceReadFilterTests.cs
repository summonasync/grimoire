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
