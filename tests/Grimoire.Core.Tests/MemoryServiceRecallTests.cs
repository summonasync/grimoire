using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryServiceRecallTests
{
    private const string QueryText = "deployment";

    private static readonly IReadOnlyDictionary<string, float[]> Vectors = new Dictionary<string, float[]>
    {
        [QueryText] = [1f, 0f, 0f],
    };

    private readonly FakeMemoryStore _store = new();

    private readonly FakeClock _clock = new();

    private readonly MemoryService _service;

    public MemoryServiceRecallTests()
    {
        _service = TestMemories.Service(_store, new FakeEmbeddingProvider(Vectors), _clock);
    }

    private static RecallQuery Query(
        int limit = 5,
        IReadOnlyList<MemoryScope>? scopes = null,
        MemoryKind? kind = null,
        IReadOnlyList<string>? tags = null,
        string? projectId = null,
        string? sessionId = null) => new()
        {
            Text = QueryText,
            Limit = limit,
            Scopes = scopes ?? [],
            Kind = kind,
            Tags = tags ?? [],
            ProjectId = projectId,
            SessionId = sessionId,
        };

    [Fact]
    public async Task Recall_RescoresBySimilarityTimesImportance()
    {
        Seed("strong", [0.9f, 0.4359f, 0f], importance: 1);
        Seed("weak", [0.5f, 0.866f, 0f], importance: 0);

        var matches = (await _service.RecallAsync(Query(), CancellationToken.None)).Matches;

        matches.Count.ShouldBe(2);
        matches[0].Score.ShouldBe(0.9, 1e-3);
        matches[1].Score.ShouldBe(0.25, 1e-3);
        matches[0].Similarity.ShouldBe(0.9, 1e-3);
    }

    [Fact]
    public async Task Recall_AppliesRecencyDecayWithScopeHalfLife()
    {
        var fresh = Seed("fresh", [0.9f, 0.4359f, 0f], importance: 1, accessed: FakeClock.DefaultStart);
        var aged = Seed("aged", [0.9f, 0.4359f, 0f], importance: 1, accessed: FakeClock.DefaultStart.AddDays(-30));

        var matches = (await _service.RecallAsync(Query(), CancellationToken.None)).Matches;

        matches[0].Record.Id.ShouldBe(fresh.Id);
        matches[1].Record.Id.ShouldBe(aged.Id);
        matches[1].Score.ShouldBe(0.45, 1e-3);
    }

    [Fact]
    public async Task Recall_ImportanceBreaksSimilarityTies()
    {
        Seed("vital", [0.8f, 0.6f, 0f], importance: 1);
        Seed("minor", [0.8f, 0.6f, 0f], importance: 0);

        var matches = (await _service.RecallAsync(Query(), CancellationToken.None)).Matches;

        matches[0].Score.ShouldBe(0.8, 1e-3);
        matches[1].Score.ShouldBe(0.4, 1e-3);
    }

    [Fact]
    public async Task Recall_ExcludesSessionScopeByDefault()
    {
        Seed("session note", [1f, 0f, 0f], scope: MemoryScope.Session);
        var project = Seed("project note", [0.9f, 0.4359f, 0f]);

        var matches = (await _service.RecallAsync(Query(), CancellationToken.None)).Matches;

        matches.Count.ShouldBe(1);
        matches[0].Record.Id.ShouldBe(project.Id);
    }

    [Fact]
    public async Task Recall_IncludesSessionScopeWhenRequested()
    {
        Seed("session note", [1f, 0f, 0f], scope: MemoryScope.Session, sessionId: "s-1");

        var matches = (await _service.RecallAsync(Query(scopes: [MemoryScope.Session], sessionId: "s-1"), CancellationToken.None)).Matches;

        matches.Count.ShouldBe(1);
        matches[0].Record.Scope.ShouldBe(MemoryScope.Session);
    }

    [Fact]
    public async Task Recall_UpdatesAccessStatsOnReturnedRecords()
    {
        var record = Seed("project note", [0.9f, 0.4359f, 0f], accessCount: 4, accessed: FakeClock.DefaultStart.AddDays(-10));
        _clock.Advance(TimeSpan.FromMinutes(3));

        var matches = (await _service.RecallAsync(Query(), CancellationToken.None)).Matches;

        matches[0].Record.AccessCount.ShouldBe(5);
        matches[0].Record.LastAccessedAt.ShouldBe(_clock.UtcNow);

        var stored = (await _store.GetAsync(record.Id, CancellationToken.None))!;
        stored.AccessCount.ShouldBe(5);
        stored.LastAccessedAt.ShouldBe(_clock.UtcNow);
    }

    [Fact]
    public async Task Recall_SkipsSupersededAndTombstonedRecords()
    {
        Seed("superseded", [1f, 0f, 0f], status: MemoryStatus.Superseded, supersededBy: Guid.NewGuid());
        Seed("tombstoned", [0.99f, 0.141f, 0f], status: MemoryStatus.Tombstoned, tombstoneReason: "forgotten", tombstonedAt: FakeClock.DefaultStart);
        var active = Seed("active", [0.5f, 0.866f, 0f]);

        var matches = (await _service.RecallAsync(Query(), CancellationToken.None)).Matches;

        matches.Count.ShouldBe(1);
        matches[0].Record.Id.ShouldBe(active.Id);
    }

    [Fact]
    public async Task Recall_RespectsLimit()
    {
        Seed("first", [0.9f, 0.4359f, 0f], importance: 1);
        Seed("second", [0.8f, 0.6f, 0f], importance: 1);
        Seed("third", [0.5f, 0.866f, 0f], importance: 1);

        var matches = (await _service.RecallAsync(Query(limit: 2), CancellationToken.None)).Matches;

        matches.Count.ShouldBe(2);
        matches[0].Similarity.ShouldBe(0.9, 1e-3);
        matches[1].Similarity.ShouldBe(0.8, 1e-3);
    }

    [Fact]
    public async Task Recall_FiltersByKind()
    {
        Seed("journal note", [1f, 0f, 0f], kind: MemoryKind.Journal);
        var fact = Seed("fact note", [0.9f, 0.4359f, 0f], kind: MemoryKind.Fact);

        var matches = (await _service.RecallAsync(Query(kind: MemoryKind.Fact), CancellationToken.None)).Matches;

        matches.Count.ShouldBe(1);
        matches[0].Record.Id.ShouldBe(fact.Id);
    }

    [Fact]
    public async Task Recall_MatchesTagsAsAnyOf()
    {
        Seed("csharp note", [1f, 0f, 0f], tags: ["csharp"]);
        Seed("rust note", [0.9f, 0.4359f, 0f], tags: ["rust"]);

        var matches = (await _service.RecallAsync(Query(tags: ["csharp", "ml"]), CancellationToken.None)).Matches;

        matches.Count.ShouldBe(1);
        matches[0].Record.Tags.ShouldBe(["csharp"]);
    }

    [Fact]
    public async Task Recall_FiltersByProject()
    {
        Seed("other project", [1f, 0f, 0f], projectId: "other");
        var own = Seed("own project", [0.9f, 0.4359f, 0f], projectId: "grim");

        var matches = (await _service.RecallAsync(Query(projectId: "grim"), CancellationToken.None)).Matches;

        matches.Count.ShouldBe(1);
        matches[0].Record.Id.ShouldBe(own.Id);
    }

    [Fact]
    public async Task Recall_OversamplingLetsRecencyBeatRawSimilarity()
    {
        Seed("stale champion", [0.99f, 0.141f, 0f], importance: 1, accessed: FakeClock.DefaultStart.AddDays(-100));
        var recent = Seed("recent runner-up", [0.7f, 0.714f, 0f], importance: 1, accessed: FakeClock.DefaultStart);

        var matches = (await _service.RecallAsync(Query(limit: 1), CancellationToken.None)).Matches;

        matches.Count.ShouldBe(1);
        matches[0].Record.Id.ShouldBe(recent.Id);
        matches[0].Score.ShouldBe(0.7, 1e-3);
    }

    [Fact]
    public async Task Recall_WithoutMatches_ReturnsEmpty()
    {
        var matches = (await _service.RecallAsync(Query(), CancellationToken.None)).Matches;

        matches.ShouldBeEmpty();
    }

    [Fact]
    public async Task Recall_FromHomeProject_DemotesForeignProjectFactsBelowHomeAndGlobal()
    {
        var home = Seed("home fact", [0.9f, 0.4359f, 0f], importance: 1, projectId: "grim");
        Seed("foreign fact", [0.9f, 0.4359f, 0f], importance: 1, projectId: "other");
        var global = Seed("global fact", [0.9f, 0.4359f, 0f], scope: MemoryScope.Global, importance: 1, projectId: null);

        var matches = (await _service.RecallAsync(Query() with { CallerProjectId = "grim" }, CancellationToken.None)).Matches;

        matches.Count.ShouldBe(3);
        matches.Take(2).ShouldContain(match => match.Record.Id == home.Id);
        matches.Take(2).ShouldContain(match => match.Record.Id == global.Id);
        matches[2].Record.ProjectId.ShouldBe("other");
        matches[0].Score.ShouldBe(0.9, 1e-3);
        matches[2].Score.ShouldBe(matches[0].Score * 0.6, 1e-3);
    }

    [Fact]
    public async Task Recall_WithoutCallerProject_ForeignFactsKeepFullScore()
    {
        Seed("home fact", [0.9f, 0.4359f, 0f], importance: 1, projectId: "grim");
        Seed("foreign fact", [0.9f, 0.4359f, 0f], importance: 1, projectId: "other");

        var matches = (await _service.RecallAsync(Query(), CancellationToken.None)).Matches;

        matches.Count.ShouldBe(2);
        matches.ShouldAllBe(match => match.Score > 0.89);
    }

    [Fact]
    public async Task Recall_EmptyText_Throws()
    {
        await Should.ThrowAsync<ArgumentException>(() => _service.RecallAsync(Query() with { Text = "" }, CancellationToken.None));
    }

    [Fact]
    public async Task Recall_NonPositiveLimit_Throws()
    {
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => _service.RecallAsync(Query(limit: 0), CancellationToken.None));
    }

    private MemoryRecord Seed(
        string content,
        float[] vector,
        MemoryScope scope = MemoryScope.Project,
        MemoryKind kind = MemoryKind.Journal,
        double importance = 0.5,
        long accessCount = 0,
        DateTimeOffset? accessed = null,
        string? projectId = "grim",
        string? sessionId = null,
        IReadOnlyList<string>? tags = null,
        MemoryStatus status = MemoryStatus.Active,
        Guid? supersededBy = null,
        string? tombstoneReason = null,
        DateTimeOffset? tombstonedAt = null)
    {
        var record = TestMemories.Create(
            content,
            vector,
            scope: scope,
            kind: kind,
            importance: importance,
            accessCount: accessCount,
            accessed: accessed,
            projectId: projectId,
            sessionId: sessionId,
            tags: tags,
            status: status,
            supersededBy: supersededBy,
            tombstoneReason: tombstoneReason,
            tombstonedAt: tombstonedAt);
        _store.Seed(record, vector);
        return record;
    }
}
