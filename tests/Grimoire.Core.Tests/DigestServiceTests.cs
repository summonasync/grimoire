using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class DigestServiceTests
{
    private static readonly IReadOnlyDictionary<string, float[]> Vectors = new Dictionary<string, float[]>
    {
        ["deploy"] = [1f, 0f, 0f],
        ["deploy-lane"] = [1f, 0f, 0f],
        ["coffee"] = [0f, 1f, 0f],
    };

    private readonly FakeMemoryStore _store = new();

    private readonly FakeClock _clock = new();

    private readonly FakeEmbeddingProvider _embeddings = new(Vectors);

    [Fact]
    public async Task Digest_EmptyStore_ReturnsExplicitEmptyStates()
    {
        var digest = await Service().DigestAsync(Query(), CancellationToken.None);

        digest.Topic.ShouldBe("topic:deploy");
        digest.LongMemoState.ShouldBe(BriefSectionState.Empty);
        digest.LongMemo.ShouldBeEmpty();
        digest.RecentState.ShouldBe(BriefSectionState.Empty);
        digest.Recent.ShouldBeEmpty();
        digest.GoneState.ShouldBe(BriefSectionState.Empty);
        digest.Gone.ShouldBeEmpty();
        digest.Omitted.ShouldBeEmpty();
    }

    [Fact]
    public async Task Digest_TaggedRecords_LandInTheMatchingSections()
    {
        SeedFact("ship gates decision", [0f, 0f, 1f], tags: ["topic:deploy"]);
        SeedFact("deploy runbook", [0f, 0f, 1f], kind: MemoryKind.Journal, tags: ["topic:deploy"]);
        SeedFact("stale deploy plan", [0f, 0f, 1f], status: MemoryStatus.Superseded,
            created: _clock.UtcNow.AddSeconds(-2), tags: ["topic:deploy"]);
        SeedFact("retracted deploy note", [0f, 0f, 1f], kind: MemoryKind.Journal, status: MemoryStatus.Tombstoned,
            created: _clock.UtcNow.AddSeconds(-1), tags: ["topic:deploy"], tombstoneReason: "wrong claim");

        var digest = await Service().DigestAsync(Query(), CancellationToken.None);

        digest.LongMemoState.ShouldBe(BriefSectionState.Populated);
        digest.LongMemo.Select(hit => hit.Content).ShouldBe(["ship gates decision"]);
        digest.LongMemo[0].Match.ShouldBe(DigestMatch.Tag);
        digest.LongMemo[0].Similarity.ShouldBeNull();
        digest.RecentState.ShouldBe(BriefSectionState.Populated);
        digest.Recent.Select(hit => hit.Content).ShouldBe(["deploy runbook"]);
        digest.GoneState.ShouldBe(BriefSectionState.Populated);
        digest.Gone.Select(hit => hit.Content).ShouldBe(["retracted deploy note", "stale deploy plan"]);
        digest.Gone.Single(hit => hit.Content == "retracted deploy note").TombstoneReason.ShouldBe("wrong claim");
        digest.Gone.Select(hit => hit.Match).ShouldBe([DigestMatch.Tag, DigestMatch.Tag]);
    }

    [Fact]
    public async Task Digest_SimilarRecordWithoutTag_CarriesTheSimilarReason()
    {
        SeedFact("untyped deploy observation", [1f, 0.7f, 0f]);
        SeedFact("untyped deploy note", [1f, 0.7f, 0f], kind: MemoryKind.Journal);

        var digest = await Service().DigestAsync(Query(), CancellationToken.None);

        digest.LongMemo.Select(hit => hit.Content).ShouldBe(["untyped deploy observation"]);
        digest.LongMemo[0].Match.ShouldBe(DigestMatch.Similar);
        digest.LongMemo[0].Similarity.ShouldNotBeNull();
        digest.LongMemo[0].Similarity!.Value.ShouldBeGreaterThanOrEqualTo(0.75);
        digest.Recent.Select(hit => hit.Content).ShouldBe(["untyped deploy note"]);
        digest.Recent[0].Match.ShouldBe(DigestMatch.Similar);
    }

    [Fact]
    public async Task Digest_LowSimilarityRecord_IsExcluded()
    {
        SeedFact("unrelated coffee chat", [0f, 1f, 0f], kind: MemoryKind.Journal);

        var digest = await Service().DigestAsync(Query(), CancellationToken.None);

        digest.LongMemo.ShouldBeEmpty();
        digest.Recent.ShouldBeEmpty();
        digest.Gone.ShouldBeEmpty();
    }

    [Fact]
    public async Task Digest_SimilarityAtExactThreshold_IsIncluded()
    {
        SeedFact("boundary deploy fact", [3f, 4f, 0f]);

        var digest = await Service().DigestAsync(Query(), CancellationToken.None);

        digest.LongMemo.ShouldHaveSingleItem();
        digest.LongMemo[0].Match.ShouldBe(DigestMatch.Similar);
        digest.LongMemo[0].Similarity!.Value.ShouldBe(0.6);
    }

    [Fact]
    public async Task Digest_SimilarityJustBelowThreshold_IsExcluded()
    {
        SeedFact("sub-threshold deploy fact", [60f, 81f, 0f]);

        var digest = await Service().DigestAsync(Query(), CancellationToken.None);

        digest.LongMemo.ShouldBeEmpty();
        digest.Recent.ShouldBeEmpty();
        digest.Gone.ShouldBeEmpty();
    }

    [Fact]
    public async Task Digest_TagAndSimilarHitOfSameRecord_DeduplicateWithTagWinning()
    {
        SeedFact("deploy gates stay green", [1f, 0.7f, 0f], tags: ["topic:deploy"]);

        var digest = await Service().DigestAsync(Query(), CancellationToken.None);

        digest.LongMemo.ShouldHaveSingleItem();
        digest.LongMemo[0].Match.ShouldBe(DigestMatch.Tag);
        digest.LongMemo[0].Similarity.ShouldBeNull();
    }

    [Fact]
    public async Task Digest_SinceUntil_BoundTheRecentWindowOnly()
    {
        var early = _clock.UtcNow.AddDays(-3);
        SeedFact("early deploy note", [0f, 0f, 1f], kind: MemoryKind.Journal, created: early);
        _clock.Advance(TimeSpan.FromDays(3));
        var late = _clock.UtcNow;
        SeedFact("late deploy note", [1f, 0.7f, 0f], kind: MemoryKind.Journal, created: late);
        SeedFact("deploy fact regardless of window", [0f, 0f, 1f], created: early, tags: ["topic:deploy"]);

        var digest = await Service().DigestAsync(Query(since: _clock.UtcNow.AddDays(-1)), CancellationToken.None);

        digest.Recent.Select(hit => hit.Content).ShouldBe(["late deploy note"]);
        digest.LongMemo.Select(hit => hit.Content).ShouldBe(["deploy fact regardless of window"]);
    }

    [Fact]
    public async Task Digest_Limit_ClampsRecentAndGoneCaps()
    {
        foreach (var index in Enumerable.Range(0, 4))
        {
            SeedFact($"deploy journal {index}", [0f, 0f, 1f], kind: MemoryKind.Journal,
                created: _clock.UtcNow.AddDays(-index), tags: ["topic:deploy"]);
            SeedFact($"deploy gone {index}", [0f, 0f, 1f], status: MemoryStatus.Superseded,
                created: _clock.UtcNow.AddDays(-index), tags: ["topic:deploy"]);
        }

        var digest = await Service().DigestAsync(Query(limit: 2), CancellationToken.None);

        digest.Recent.Count.ShouldBe(2);
        digest.Recent.Select(hit => hit.Content).ShouldBe(["deploy journal 0", "deploy journal 1"]);
        digest.Gone.Count.ShouldBe(2);
        digest.Gone.Select(hit => hit.Content).ShouldBe(["deploy gone 0", "deploy gone 1"]);
    }

    [Fact]
    public async Task Digest_SectionOverBudget_IsOmittedAndLaterSectionsFill()
    {
        var options = new MemoryEngineOptions { DigestMaxTotalChars = 60 };
        SeedFact(new string('d', 100), [0f, 0f, 1f], tags: ["topic:deploy"]);
        SeedFact("small deploy journal", [0f, 0f, 1f], kind: MemoryKind.Journal, tags: ["topic:deploy"]);

        var digest = await Service(options).DigestAsync(Query(), CancellationToken.None);

        digest.LongMemoState.ShouldBe(BriefSectionState.Omitted);
        digest.LongMemo.ShouldBeEmpty();
        digest.Omitted.ShouldBe([DigestSection.LongMemo]);
        digest.RecentState.ShouldBe(BriefSectionState.Populated);
        digest.Recent.Select(hit => hit.Content).ShouldBe(["small deploy journal"]);
    }

    [Fact]
    public async Task Digest_LongEntry_IsTruncatedAndFlagged()
    {
        SeedFact(new string('d', 500), [0f, 0f, 1f], tags: ["topic:deploy"]);
        SeedFact("short deploy fact", [0f, 0f, 1f], created: _clock.UtcNow.AddSeconds(1), tags: ["topic:deploy"]);

        var digest = await Service().DigestAsync(Query(), CancellationToken.None);

        var truncated = digest.LongMemo.Single(hit => hit.Truncated);
        truncated.Content.Length.ShouldBe(400);
        truncated.Content.ShouldBe(new string('d', 400));
        digest.LongMemo.Single(hit => hit.Content == "short deploy fact").Truncated.ShouldBeFalse();
    }

    [Fact]
    public async Task Digest_HitsBumpAccessStatsOnTheStore()
    {
        var bumpedAt = _clock.UtcNow.AddDays(-5);
        var seeded = SeedFact("deploy fact to bump", [0f, 0f, 1f], tags: ["topic:deploy"],
            accessed: bumpedAt, accessCount: 2);
        SeedFact(new string('x', 6000), [0f, 0f, 1f], created: _clock.UtcNow.AddSeconds(1), tags: ["topic:deploy"]);

        var digest = await Service(new MemoryEngineOptions { DigestMaxTotalChars = 100 }).DigestAsync(Query(), CancellationToken.None);

        digest.LongMemoState.ShouldBe(BriefSectionState.Omitted);
        digest.RecentState.ShouldBe(BriefSectionState.Empty);
        var stored = _store.Records.Single(record => record.Id == seeded.Id);
        stored.AccessCount.ShouldBe(2, "an omitted section must not bump its records");
    }

    [Fact]
    public async Task Digest_PopulatedHits_BumpAccessCountAndLastAccessed()
    {
        var seeded = SeedFact("deploy fact to bump", [0f, 0f, 1f], tags: ["topic:deploy"], accessCount: 2);

        var digest = await Service().DigestAsync(Query(), CancellationToken.None);

        var now = _clock.UtcNow;
        digest.LongMemo[0].LastAccessedAt.ShouldBe(now);
        digest.LongMemo[0].Id.ShouldBe(seeded.Id);
        var stored = _store.Records.Single(record => record.Id == seeded.Id);
        stored.AccessCount.ShouldBe(3);
        stored.LastAccessedAt.ShouldBe(now);
    }

    [Fact]
    public async Task Digest_SessionScopeRecords_AreExcluded()
    {
        SeedFact("session deploy scratchpad", [0f, 0f, 1f], scope: MemoryScope.Session, tags: ["topic:deploy"]);

        var digest = await Service().DigestAsync(Query(), CancellationToken.None);

        digest.LongMemoState.ShouldBe(BriefSectionState.Empty);
    }

    [Fact]
    public async Task Digest_PersonaRecords_AreExcluded()
    {
        SeedFact("persona deploy row", [0f, 0f, 1f], tags: ["persona", "topic:deploy"]);

        var digest = await Service().DigestAsync(Query(), CancellationToken.None);

        digest.LongMemoState.ShouldBe(BriefSectionState.Empty);
    }

    [Fact]
    public async Task Digest_OtherTomeRecords_AreExcluded()
    {
        SeedFact("foreign tome deploy fact", [0f, 0f, 1f], tags: ["topic:deploy"], tome: "other-tome");

        var digest = await Service().DigestAsync(Query(), CancellationToken.None);

        digest.LongMemoState.ShouldBe(BriefSectionState.Empty);
    }

    [Fact]
    public async Task Digest_BlankTopic_Throws()
    {
        await Should.ThrowAsync<ArgumentException>(async () =>
            await Service().DigestAsync(Query(topic: "   "), CancellationToken.None));
    }

    [Fact]
    public async Task Digest_CrossLingualTopic_UsesTheTopicEmbeddingOnly()
    {
        _embeddings.Requests.Clear();
        SeedFact("部署纪要", [1f, 0.7f, 0f], kind: MemoryKind.Journal);

        var digest = await Service().DigestAsync(Query(topic: "Deploy Lane"), CancellationToken.None);

        digest.Topic.ShouldBe("topic:deploy-lane");
        _embeddings.Requests.ShouldBe(["deploy-lane"]);
        digest.Recent.Select(hit => hit.Content).ShouldBe(["部署纪要"]);
    }

    private DigestService Service(MemoryEngineOptions? options = null) => new(_store, _embeddings, _clock, options);

    private static DigestQuery Query(
        string topic = "deploy",
        string? tome = null,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null,
        int? limit = null) => new()
        {
            Topic = topic,
            Tome = tome,
            CreatedAfter = since,
            CreatedBefore = until,
            Limit = limit,
        };

    private MemoryRecord SeedFact(
        string content,
        float[] vector,
        MemoryKind kind = MemoryKind.Fact,
        MemoryScope scope = MemoryScope.Project,
        MemoryStatus status = MemoryStatus.Active,
        IReadOnlyList<string>? tags = null,
        DateTimeOffset? created = null,
        DateTimeOffset? accessed = null,
        long accessCount = 0,
        string? tombstoneReason = null,
        string? tome = null)
    {
        var record = TestMemories.Create(
            content,
            vector,
            scope: scope,
            kind: kind,
            created: created,
            accessed: accessed,
            accessCount: accessCount,
            tags: tags,
            status: status,
            tombstoneReason: tombstoneReason,
            tome: tome);
        _store.Seed(record, vector);
        return record;
    }
}
