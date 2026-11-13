using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Grimoire.Infrastructure.Tests.Fakes;
using Grpc.Core;
using Qdrant.Client.Grpc;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Memory;

public sealed class QdrantMemoryStoreTests
{
    private const int PointsPerScrollPage = 256;
    private const int SeededPointCount = PointsPerScrollPage + 1;

    private static readonly Guid IdB = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid IdD = Guid.Parse("00000000-0000-0000-0000-00000000000d");

    [Fact]
    public async Task List_WithUnconstrainedFilter_ReturnsOnlyActiveRecords()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var active = await SeedAsync(store, TestMemories.Create("active"));
        await SeedAsync(store, TestMemories.Create("superseded", status: MemoryStatus.Superseded, supersededBy: active.Id));
        await SeedAsync(store, TestMemories.Create("tombstoned", status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: TestMemories.Moment));

        var records = await store.ListAsync(new MemoryFilter(), CancellationToken.None);

        records.Select(record => record.Id).ShouldBe([active.Id]);
    }

    [Fact]
    public async Task Search_WithUnconstrainedFilter_ExcludesTombstonedAndSuperseded()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var active = await SeedAsync(store, TestMemories.Create("active"), 1, 0, 0, 0);
        await SeedAsync(store, TestMemories.Create("superseded", status: MemoryStatus.Superseded), 0, 1, 0, 0);
        await SeedAsync(store, TestMemories.Create("tombstoned", status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: TestMemories.Moment), 0, 0, 1, 0);

        var matches = await store.SearchAsync(new float[] { 1, 0, 0, 0 }, new MemoryFilter(), 10, CancellationToken.None);

        matches.Select(match => match.Record.Id).ShouldBe([active.Id]);
        matches.Single().Similarity.ShouldBe(1.0, 1e-6);
    }

    [Fact]
    public async Task Fetch_WithUnconstrainedFilter_ExcludesNonActiveAndCarriesVectors()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var active = await SeedAsync(store, TestMemories.Create("active"), 1, 0, 0, 0);
        await SeedAsync(store, TestMemories.Create("superseded", status: MemoryStatus.Superseded), 0, 1, 0, 0);
        await SeedAsync(store, TestMemories.Create("tombstoned", status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: TestMemories.Moment), 0, 0, 1, 0);

        var points = await store.FetchAsync(new MemoryFilter(), CancellationToken.None);

        var point = points.ShouldHaveSingleItem();
        point.Record.Id.ShouldBe(active.Id);
        point.Vector.ToArray().ShouldBe(new float[] { 1, 0, 0, 0 });
    }

    [Fact]
    public async Task Count_WithUnconstrainedFilter_CountsOnlyActive()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var active = await SeedAsync(store, TestMemories.Create("active"));
        await SeedAsync(store, TestMemories.Create("superseded", status: MemoryStatus.Superseded, supersededBy: active.Id));
        await SeedAsync(store, TestMemories.Create("tombstoned", status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: TestMemories.Moment));

        var count = await store.CountAsync(new MemoryFilter(), CancellationToken.None);

        count.ShouldBe(1);
    }

    [Fact]
    public async Task List_WithCreatedBefore_ExcludesTheRecordAtTheBoundAndKeepsTheEarlierOne()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var boundary = TestMemories.Moment;
        var atBound = await SeedAsync(store, TestMemories.Create("at-bound", created: boundary));
        var oneTickBefore = await SeedAsync(store, TestMemories.Create("one-tick-before", created: boundary.AddTicks(-1)));
        var oneTickAfter = await SeedAsync(store, TestMemories.Create("one-tick-after", created: boundary.AddTicks(1)));

        var ids = await ListIdsAsync(store, new MemoryFilter { CreatedBefore = boundary });

        // The earlier record has to be present for the exclusion below to mean anything.
        ids.ShouldContain(oneTickBefore.Id);
        ids.ShouldNotContain(atBound.Id);
        ids.ShouldNotContain(oneTickAfter.Id);
    }

    [Fact]
    public async Task Count_WithCreatedBefore_CountsOnlyRecordsStrictlyBeforeTheBound()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var boundary = TestMemories.Moment;
        await SeedAsync(store, TestMemories.Create("at-bound", created: boundary));
        await SeedAsync(store, TestMemories.Create("one-tick-before", created: boundary.AddTicks(-1)));
        await SeedAsync(store, TestMemories.Create("a-day-before", created: boundary.AddDays(-1)));

        var count = await store.CountAsync(new MemoryFilter { CreatedBefore = boundary }, CancellationToken.None);

        count.ShouldBe(2);
    }

    [Fact]
    public async Task List_WithCreatedBeforeInAnotherOffset_BoundsTheUtcInstantNotTheLocalTicks()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var boundary = TestMemories.Moment;
        await SeedAsync(store, TestMemories.Create("at-bound", created: boundary));
        var oneTickBefore = await SeedAsync(store, TestMemories.Create("one-tick-before", created: boundary.AddTicks(-1)));

        // The same instant spelled as 15:00+03:00: the bound must be read as an instant, not as
        // whatever wall-clock digits the offset representation happens to carry.
        var ids = await ListIdsAsync(store, new MemoryFilter { CreatedBefore = boundary.ToOffset(TimeSpan.FromHours(3)) });

        ids.ShouldContain(oneTickBefore.Id);
        ids.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Window_WithBothCreatedBounds_ReadsAndCountsTheSameSet()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var from = TestMemories.Moment;
        var until = TestMemories.Moment.AddHours(6);
        var beforeWindow = await SeedAsync(store, TestMemories.Create("before-window", created: from.AddSeconds(-1)));
        var atFrom = await SeedAsync(store, TestMemories.Create("at-from", created: from));
        var inside = await SeedAsync(store, TestMemories.Create("inside", created: from.AddHours(3)));
        await SeedAsync(store, TestMemories.Create("at-until", created: until));
        await SeedAsync(store, TestMemories.Create("after-until", created: until.AddSeconds(1)));

        var filter = new MemoryFilter { CreatedAfter = from, CreatedBefore = until };
        var ids = await ListIdsAsync(store, filter);
        var count = await store.CountAsync(filter, CancellationToken.None);

        ids.ShouldContain(atFrom.Id);
        ids.ShouldContain(inside.Id);
        ids.ShouldNotContain(beforeWindow.Id);
        ids.Count.ShouldBe(2);
        count.ShouldBe(ids.Count);
    }

    [Fact]
    public async Task Get_ById_ReturnsRawRecordWithAnyStatus()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var tombstoned = await SeedAsync(
            store,
            TestMemories.Create("gone", status: MemoryStatus.Tombstoned, tombstoneReason: "user asked", tombstonedAt: TestMemories.Moment));

        var loaded = await store.GetAsync(tombstoned.Id, CancellationToken.None);

        loaded.ShouldNotBeNull();
        loaded.Status.ShouldBe(MemoryStatus.Tombstoned);
        (await store.GetAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task Upsert_StampsTheConfiguredVectorModelOntoThePoint()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var record = TestMemories.Create("stamped");
        float[] vector = [1f, 0, 0, 0];

        await store.UpsertAsync(record, vector, CancellationToken.None);

        db.PayloadOf(record.Id)[MemoryPayloadFields.VectorModel].StringValue.ShouldBe("nomic-embed-text");
    }

    [Fact]
    public async Task Get_ById_RoundTripsEveryPayloadField()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var source = Guid.NewGuid();
        var superseded = Guid.NewGuid();
        var record = TestMemories.Create(
            "full body",
            scope: MemoryScope.Session,
            kind: MemoryKind.Fact,
            created: TestMemories.Moment,
            accessed: TestMemories.Moment.AddMinutes(5),
            importance: 0.75,
            accessCount: 3,
            projectId: "proj-1",
            sessionId: "sess-1",
            tags: ["alpha", "beta"],
            status: MemoryStatus.Tombstoned,
            expiresAt: TestMemories.Moment.AddHours(2),
            supersededBy: superseded,
            tombstoneReason: "user asked",
            tombstonedAt: TestMemories.Moment.AddMinutes(9),
            sources: [source],
            idempotencyKey: "key-42");
        await store.UpsertAsync(record, new float[] { 0.5f, 0.5f, 0.5f, 0.5f }, CancellationToken.None);

        var loaded = await store.GetAsync(record.Id, CancellationToken.None);

        loaded.ShouldNotBeNull();
        loaded.Id.ShouldBe(record.Id);
        loaded.Content.ShouldBe("full body");
        loaded.Scope.ShouldBe(MemoryScope.Session);
        loaded.Kind.ShouldBe(MemoryKind.Fact);
        loaded.Status.ShouldBe(MemoryStatus.Tombstoned);
        loaded.Actor.Alias.ShouldBe(TestMemories.Agent.Alias);
        loaded.Actor.Vendor.ShouldBe(TestMemories.Agent.Vendor);
        loaded.ProjectId.ShouldBe("proj-1");
        loaded.SessionId.ShouldBe("sess-1");
        loaded.Tags.ShouldBe(["alpha", "beta"]);
        loaded.Importance.ShouldBe(0.75);
        loaded.CreatedAt.ShouldBe(TestMemories.Moment);
        loaded.LastAccessedAt.ShouldBe(TestMemories.Moment.AddMinutes(5));
        loaded.AccessCount.ShouldBe(3);
        loaded.ExpiresAt.ShouldBe(TestMemories.Moment.AddHours(2));
        loaded.SupersededBy.ShouldBe(superseded);
        loaded.TombstoneReason.ShouldBe("user asked");
        loaded.TombstonedAt.ShouldBe(TestMemories.Moment.AddMinutes(9));
        loaded.Sources.ShouldBe([source]);
        loaded.IdempotencyKey.ShouldBe("key-42");
    }

    [Fact]
    public async Task Update_WithAccessStats_RewritesPayloadWithoutUpsertingVector()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var record = await SeedAsync(store, TestMemories.Create("original", projectId: "p1"), 1, 0, 0, 0);
        var updated = record with
        {
            AccessCount = 5,
            LastAccessedAt = TestMemories.Moment.AddHours(1),
            Status = MemoryStatus.Superseded,
            SupersededBy = Guid.NewGuid(),
        };
        var upsertsBefore = db.UpsertCount;

        await store.UpdateAsync(updated, CancellationToken.None);

        db.UpsertCount.ShouldBe(upsertsBefore);
        db.SetPayloads.ShouldHaveSingleItem();
        db.SetPayloads[0].ShouldContainKey(MemoryPayloadFields.AccessCount);
        db.SetPayloads[0][MemoryPayloadFields.AccessCount].IntegerValue.ShouldBe(5);

        var loaded = await store.GetAsync(record.Id, CancellationToken.None);
        loaded.ShouldNotBeNull();
        loaded.AccessCount.ShouldBe(5);
        loaded.Status.ShouldBe(MemoryStatus.Superseded);
        loaded.ProjectId.ShouldBe("p1");

        var points = await store.FetchAsync(new MemoryFilter { Status = MemoryStatus.Superseded }, CancellationToken.None);
        points.ShouldHaveSingleItem().Vector.ToArray().ShouldBe(new float[] { 1, 0, 0, 0 });
    }

    [Fact]
    public async Task List_ReturnsCreatedAtDescThenIdAsc()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var oldest = await SeedAsync(store, TestMemories.Create("oldest", created: TestMemories.Moment.AddHours(1)));
        var mid = await SeedAsync(store, TestMemories.Create("mid", created: TestMemories.Moment.AddHours(2)));
        var newer = await SeedAsync(store, TestMemories.Create("newer", created: TestMemories.Moment.AddHours(3), id: IdD));
        var tie = await SeedAsync(store, TestMemories.Create("tie", created: TestMemories.Moment.AddHours(3), id: IdB));

        var records = await store.ListAsync(new MemoryFilter(), CancellationToken.None);

        records.Select(record => record.Id).ShouldBe([tie.Id, newer.Id, mid.Id, oldest.Id]);
    }

    [Fact]
    public async Task List_WithLimit_AppliesLimitAfterOrdering()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var oldest = await SeedAsync(store, TestMemories.Create("oldest", created: TestMemories.Moment.AddHours(1)));
        var mid = await SeedAsync(store, TestMemories.Create("mid", created: TestMemories.Moment.AddHours(2)));
        var newer = await SeedAsync(store, TestMemories.Create("newer", created: TestMemories.Moment.AddHours(3), id: IdD));
        var tie = await SeedAsync(store, TestMemories.Create("tie", created: TestMemories.Moment.AddHours(3), id: IdB));

        var records = await store.ListAsync(new MemoryFilter { Limit = 2 }, CancellationToken.None);

        records.Select(record => record.Id).ShouldBe([tie.Id, newer.Id]);
        records.ShouldNotContain(record => record.Id == mid.Id || record.Id == oldest.Id);
    }

    [Fact]
    public async Task List_WithMorePointsThanOneScrollPage_ReturnsEveryRecordExactlyOnce()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var ids = new List<Guid>();

        for (var i = 0; i < SeededPointCount; i++)
        {
            var record = TestMemories.Create($"point {i}");
            await store.UpsertAsync(record, new float[] { 1, 0, 0, 0 }, CancellationToken.None);
            ids.Add(record.Id);
        }

        var records = await store.ListAsync(new MemoryFilter(), CancellationToken.None);
        var points = await store.FetchAsync(new MemoryFilter(), CancellationToken.None);
        var count = await store.CountAsync(new MemoryFilter(), CancellationToken.None);

        db.ScrolledFilters.Count.ShouldBeGreaterThanOrEqualTo(2);
        records.Select(record => record.Id).ShouldBe(ids, ignoreOrder: true);
        points.Select(point => point.Record.Id).ShouldBe(ids, ignoreOrder: true);
        count.ShouldBe(SeededPointCount);
    }

    [Fact]
    public async Task List_WithComposedFilter_ConstrainsEverySetMember()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var r1 = await SeedAsync(store, TestMemories.Create("r1", projectId: "p1", sessionId: "s1", tags: ["alpha"]));
        var r2 = await SeedAsync(store, TestMemories.Create("r2", projectId: "p1", sessionId: "s2", tags: ["alpha", "beta"]));
        var r3 = await SeedAsync(store, TestMemories.Create("r3", kind: MemoryKind.Fact, projectId: "p2", tags: ["beta"]));
        var r4 = await SeedAsync(store, TestMemories.Create("r4", scope: MemoryScope.Session, projectId: "p1", sessionId: "s1", tags: ["alpha"]));
        var r5 = await SeedAsync(store, TestMemories.Create("r5", scope: MemoryScope.Global, projectId: "p1"));

        (await ListIdsAsync(store, new MemoryFilter { Scopes = [MemoryScope.Project, MemoryScope.Session] }))
            .ShouldBe(new[] { r1.Id, r2.Id, r3.Id, r4.Id }, ignoreOrder: true);
        (await ListIdsAsync(store, new MemoryFilter { Kind = MemoryKind.Fact }))
            .ShouldBe([r3.Id]);
        (await ListIdsAsync(store, new MemoryFilter { Tags = ["alpha"] }))
            .ShouldBe(new[] { r1.Id, r2.Id, r4.Id }, ignoreOrder: true);
        (await ListIdsAsync(store, new MemoryFilter { ExcludeTags = ["alpha"] }))
            .ShouldBe(new[] { r3.Id, r5.Id }, ignoreOrder: true);
        (await ListIdsAsync(store, new MemoryFilter { ProjectId = "p1" }))
            .ShouldBe(new[] { r1.Id, r2.Id, r4.Id, r5.Id }, ignoreOrder: true);
        (await ListIdsAsync(store, new MemoryFilter { SessionId = "s1" }))
            .ShouldBe(new[] { r1.Id, r4.Id }, ignoreOrder: true);
        (await ListIdsAsync(store, new MemoryFilter { ProjectId = "p1", Kind = MemoryKind.Journal, Tags = ["alpha"], SessionId = "s1" }))
            .ShouldBe(new[] { r1.Id, r4.Id }, ignoreOrder: true);
    }

    [Fact]
    public async Task List_WithNullFilterMembers_LeavesThemUnconstrained()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var withProject = await SeedAsync(store, TestMemories.Create("withProject", projectId: "p1", sessionId: "s1"));
        var withoutProject = await SeedAsync(store, TestMemories.Create("withoutProject"));

        var records = await store.ListAsync(new MemoryFilter(), CancellationToken.None);

        records.Select(record => record.Id).ShouldBe(new[] { withProject.Id, withoutProject.Id }, ignoreOrder: true);
    }

    [Fact]
    public async Task List_WithTomeFilter_IsolatesTomes()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var alpha = await SeedAsync(store, TestMemories.Create("alpha", tome: "alpha"));
        var fallback = await SeedAsync(store, TestMemories.Create("fallback"));

        (await ListIdsAsync(store, new MemoryFilter { Tome = "alpha" })).ShouldBe([alpha.Id]);
        (await ListIdsAsync(store, new MemoryFilter { Tome = TomeKey.DefaultTome })).ShouldBe([fallback.Id]);
    }

    [Fact]
    public async Task List_WithLegacyPointWithoutPayloadTome_ResolvesItIntoDefaultTome()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var modern = await SeedAsync(store, TestMemories.Create("modern"));
        var legacy = TestMemories.Create("legacy");
        var payload = new Dictionary<string, Value>(MemoryPayloadMapper.ToPayload(legacy));
        payload.Remove(MemoryPayloadFields.Tome);
        db.Seed(legacy.Id, [1f, 0f], payload);

        var defaults = await store.ListAsync(new MemoryFilter { Tome = TomeKey.DefaultTome }, CancellationToken.None);
        var alpha = await store.ListAsync(new MemoryFilter { Tome = "alpha" }, CancellationToken.None);

        defaults.Select(record => record.Id).ShouldBe(new[] { modern.Id, legacy.Id }, ignoreOrder: true);
        defaults.Single(record => record.Id == legacy.Id).Tome.ShouldBe(TomeKey.DefaultTome);
        alpha.ShouldBeEmpty();
    }

    [Fact]
    public async Task Count_WithTomeFilter_GroupsLegacyPointsIntoDefaultTome()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        await SeedAsync(store, TestMemories.Create("modern default"));
        await SeedAsync(store, TestMemories.Create("modern alpha", tome: "alpha"));
        var legacy = TestMemories.Create("legacy");
        var payload = new Dictionary<string, Value>(MemoryPayloadMapper.ToPayload(legacy));
        payload.Remove(MemoryPayloadFields.Tome);
        db.Seed(legacy.Id, [1f, 0f], payload);

        var defaultCount = await store.CountAsync(new MemoryFilter { Tome = TomeKey.DefaultTome }, CancellationToken.None);
        var alphaCount = await store.CountAsync(new MemoryFilter { Tome = "alpha" }, CancellationToken.None);

        defaultCount.ShouldBe(2);
        alphaCount.ShouldBe(1);
    }

    [Fact]
    public async Task Get_WithLegacyPointWithoutPayloadTome_DecodesAsDefaultTome()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        await SeedAsync(store, TestMemories.Create("modern"));
        var legacy = TestMemories.Create("legacy");
        var payload = new Dictionary<string, Value>(MemoryPayloadMapper.ToPayload(legacy));
        payload.Remove(MemoryPayloadFields.Tome);
        db.Seed(legacy.Id, [1f, 0f], payload);

        var loaded = await store.GetAsync(legacy.Id, CancellationToken.None);

        loaded.ShouldNotBeNull();
        loaded.Tome.ShouldBe(TomeKey.DefaultTome);
    }

    [Fact]
    public async Task List_WithExpiresBeforeBoundary_IncludesEqualTimestamp()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var exact = await SeedAsync(store, TestMemories.Create("exact", scope: MemoryScope.Session, expiresAt: TestMemories.Moment));
        await SeedAsync(store, TestMemories.Create("later", scope: MemoryScope.Session, expiresAt: TestMemories.Moment.AddHours(1)));
        await SeedAsync(store, TestMemories.Create("never", scope: MemoryScope.Project));

        var records = await store.ListAsync(new MemoryFilter { Scopes = [MemoryScope.Session], ExpiresBefore = TestMemories.Moment }, CancellationToken.None);

        records.Select(record => record.Id).ShouldBe([exact.Id]);
    }

    [Fact]
    public async Task List_WithTombstonedBefore_ReturnsOnlyAgedTombstones()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var aged = await SeedAsync(store, TestMemories.Create("aged", status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: TestMemories.Moment));
        await SeedAsync(store, TestMemories.Create("fresh", status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: TestMemories.Moment.AddHours(1)));
        await SeedAsync(store, TestMemories.Create("active"));

        var records = await store.ListAsync(
            new MemoryFilter { Status = MemoryStatus.Tombstoned, TombstonedBefore = TestMemories.Moment },
            CancellationToken.None);

        records.Select(record => record.Id).ShouldBe([aged.Id]);
    }

    [Fact]
    public async Task List_WithReplayLookupShape_ReturnsOnlyKeyedRecordInsideWindow()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var keyed = await SeedAsync(store, TestMemories.Create("keyed", created: TestMemories.Moment.AddHours(2), idempotencyKey: "key-1"));
        await SeedAsync(store, TestMemories.Create("stale", created: TestMemories.Moment.AddHours(-30), idempotencyKey: "key-1"));
        await SeedAsync(store, TestMemories.Create("otherKey", created: TestMemories.Moment.AddHours(1), idempotencyKey: "key-2"));
        await SeedAsync(store, TestMemories.Create("unkeyed", created: TestMemories.Moment.AddHours(3)));

        var records = await store.ListAsync(
            new MemoryFilter { IdempotencyKey = "key-1", CreatedAfter = TestMemories.Moment },
            CancellationToken.None);

        records.Select(record => record.Id).ShouldBe([keyed.Id]);
    }

    [Fact]
    public async Task List_WithCreatedAfterBoundary_IncludesEqualTimestamp()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var boundary = await SeedAsync(store, TestMemories.Create("boundary", created: TestMemories.Moment));
        await SeedAsync(store, TestMemories.Create("older", created: TestMemories.Moment.AddHours(-1)));

        var records = await store.ListAsync(new MemoryFilter { CreatedAfter = TestMemories.Moment }, CancellationToken.None);

        records.Select(record => record.Id).ShouldBe([boundary.Id]);
    }

    [Fact]
    public async Task List_ExpiredSessions_LeavesTombstonedSessionsOut()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var expiredActive = await SeedAsync(store, TestMemories.Create("expired", scope: MemoryScope.Session, expiresAt: TestMemories.Moment));
        await SeedAsync(store, TestMemories.Create(
            "expiredAndForgotten",
            scope: MemoryScope.Session,
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "gone",
            tombstonedAt: TestMemories.Moment,
            expiresAt: TestMemories.Moment));

        var records = await store.ListAsync(
            new MemoryFilter { Scopes = [MemoryScope.Session], ExpiresBefore = TestMemories.Moment },
            CancellationToken.None);

        records.Select(record => record.Id).ShouldBe([expiredActive.Id]);
    }

    [Fact]
    public async Task Search_WithTagsFilter_SupportsDuplicateProbeExclusion()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var persona = await SeedAsync(store, TestMemories.Create("persona", tags: ["persona"]), 1, 0, 0, 0);
        var ordinary = await SeedAsync(store, TestMemories.Create("ordinary"), 0.9f, 0.1f, 0, 0);

        var probe = await store.SearchAsync(
            new float[] { 1, 0, 0, 0 },
            new MemoryFilter { ExcludeTags = ["persona"] },
            5,
            CancellationToken.None);
        var tagged = await store.SearchAsync(new float[] { 1, 0, 0, 0 }, new MemoryFilter { Tags = ["persona"] }, 5, CancellationToken.None);

        probe.Select(match => match.Record.Id).ShouldBe([ordinary.Id]);
        tagged.Select(match => match.Record.Id).ShouldBe([persona.Id]);
    }

    [Fact]
    public async Task Search_WithLimit_ReturnsAtMostLimitMatches()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        await SeedAsync(store, TestMemories.Create("one"), 1, 0, 0, 0);
        await SeedAsync(store, TestMemories.Create("two"), 0.9f, 0.1f, 0, 0);

        var matches = await store.SearchAsync(new float[] { 1, 0, 0, 0 }, new MemoryFilter(), 1, CancellationToken.None);

        matches.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Delete_RemovesSelectedPointsAndIgnoresEmptyBatch()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var first = await SeedAsync(store, TestMemories.Create("first"));
        var second = await SeedAsync(store, TestMemories.Create("second"));

        await store.DeleteAsync([first.Id], CancellationToken.None);

        (await store.GetAsync(first.Id, CancellationToken.None)).ShouldBeNull();
        (await store.GetAsync(second.Id, CancellationToken.None)).ShouldNotBeNull();
        (await store.CountAsync(new MemoryFilter(), CancellationToken.None)).ShouldBe(1);
        db.DeletedBatches.ShouldHaveSingleItem();

        await store.DeleteAsync([], CancellationToken.None);
        db.DeletedBatches.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Store_WhenCollectionMissing_RefusesOperationsWithoutRecreatingIt()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        db.CollectionExists = false;

        var exception = await Should.ThrowAsync<ReindexRequiredException>(
            () => store.ListAsync(new MemoryFilter(), CancellationToken.None));

        db.CreateCollectionCount.ShouldBe(0);
        exception.Message.ShouldContain("missing");
        exception.Message.ShouldContain("`grimoire init`");
    }

    [Fact]
    public async Task Store_WhenCollectionDisappearsMidSession_RefusesWithReindexRequired()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        await store.CountAsync(new MemoryFilter(), CancellationToken.None);
        db.Failure = new RpcException(new Status(StatusCode.NotFound, "collection not found"));

        var exception = await Should.ThrowAsync<ReindexRequiredException>(
            () => store.CountAsync(new MemoryFilter(), CancellationToken.None));

        exception.Message.ShouldContain("dropped");
    }

    [Fact]
    public async Task Store_WhenEmbeddingModelDiffers_ThrowsReindexRequiredOnFirstUse()
    {
        var db = new FakeQdrantVectorDb();
        db.Seed(
            EmbeddingFingerprint.MarkerPointId,
            new float[4],
            EmbeddingFingerprint.From("other-model", 4).ToPayload());
        var store = NewStore(db);

        var exception = await Should.ThrowAsync<ReindexRequiredException>(
            () => store.ListAsync(new MemoryFilter(), CancellationToken.None));

        exception.Message.ShouldContain("other-model");
        exception.Message.ShouldContain("run `grimoire reindex`");
    }

    [Fact]
    public async Task Store_WhenEmbeddingDimensionsDiffers_ThrowsReindexRequiredOnFirstUse()
    {
        var db = new FakeQdrantVectorDb();
        db.Seed(
            EmbeddingFingerprint.MarkerPointId,
            new float[8],
            EmbeddingFingerprint.From("nomic-embed-text", 8).ToPayload());
        var store = NewStore(db);

        await Should.ThrowAsync<ReindexRequiredException>(
            () => store.ListAsync(new MemoryFilter(), CancellationToken.None));
    }

    [Fact]
    public async Task Store_WhenReindexInProgress_RefusesOperationsUntilMarkerClears()
    {
        var db = new FakeQdrantVectorDb();
        db.Seed(
            EmbeddingFingerprint.MarkerPointId,
            new float[4],
            WithReindexState(EmbeddingFingerprint.From("nomic-embed-text", 4)));
        var store = NewStore(db);

        await Should.ThrowAsync<ReindexRequiredException>(
            () => store.ListAsync(new MemoryFilter(), CancellationToken.None));

        db.Seed(EmbeddingFingerprint.MarkerPointId, new float[4], EmbeddingFingerprint.From("nomic-embed-text", 4).ToPayload());

        await store.ListAsync(new MemoryFilter(), CancellationToken.None);
    }

    [Fact]
    public async Task DeleteByTome_RemovesEveryStatusInTheTomeAndReturnsPerStatusCounts()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        await SeedAsync(store, TestMemories.Create("active", tome: "dev"));
        await SeedAsync(store, TestMemories.Create("superseded", tome: "dev", status: MemoryStatus.Superseded, supersededBy: Guid.NewGuid()));
        await SeedAsync(store, TestMemories.Create("tombstoned", tome: "dev", status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: TestMemories.Moment));
        await SeedAsync(store, TestMemories.Create("crew member", tome: "crew"));

        var result = await store.DeleteByTomeAsync("dev", CancellationToken.None);

        result.Active.ShouldBe(1);
        result.Superseded.ShouldBe(1);
        result.Tombstoned.ShouldBe(1);
        db.DeletedBatches.ShouldHaveSingleItem();
        (await store.ListAsync(
            new MemoryFilter
            {
                Tome = "dev",
                Statuses = [MemoryStatus.Active, MemoryStatus.Superseded, MemoryStatus.Tombstoned],
            },
            CancellationToken.None)).ShouldBeEmpty();
        (await store.ListAsync(new MemoryFilter { Tome = "crew" }, CancellationToken.None)).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task DeleteByTome_UsesAStrictlyPositiveTomeFilterWithoutMustNot()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        await SeedAsync(store, TestMemories.Create("active", tome: "dev"));

        await store.DeleteByTomeAsync("dev", CancellationToken.None);

        var filter = db.ScrolledFilters.ShouldHaveSingleItem();
        filter.MustNot.ShouldBeEmpty();
        var condition = filter.Must.ShouldHaveSingleItem();
        condition.Field.ShouldNotBeNull().Key.ShouldBe(MemoryPayloadFields.Tome);
        condition.Field.Match.Keyword.ShouldBe("dev");
        condition.IsEmpty.ShouldBeNull();
        condition.Filter.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteByTome_WithDefaultTome_NeverDeletesPointsWithoutATomePayload()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var explicitDefault = await SeedAsync(store, TestMemories.Create("explicit default"));
        var legacy = TestMemories.Create("legacy");
        var legacyPayload = new Dictionary<string, Value>(MemoryPayloadMapper.ToPayload(legacy));
        legacyPayload.Remove(MemoryPayloadFields.Tome);
        db.Seed(legacy.Id, [1f, 0f], legacyPayload);
        db.Seed(
            EmbeddingFingerprint.MarkerPointId,
            new float[4],
            EmbeddingFingerprint.From("nomic-embed-text", 4).ToPayload());

        var result = await store.DeleteByTomeAsync(TomeKey.DefaultTome, CancellationToken.None);

        result.Active.ShouldBe(1);
        result.Superseded.ShouldBe(0);
        result.Tombstoned.ShouldBe(0);
        db.DeletedBatches.ShouldHaveSingleItem().ShouldBe([explicitDefault.Id]);
        db.PointIds.ShouldContain(EmbeddingFingerprint.MarkerPointId);
        db.PointIds.ShouldContain(legacy.Id);
    }

    private static IReadOnlyDictionary<string, Value> WithReindexState(EmbeddingFingerprint fingerprint)
    {
        var payload = new Dictionary<string, Value>(fingerprint.ToPayload())
        {
            [EmbeddingFingerprint.ReindexStateField] = EmbeddingFingerprint.ReindexStateInProgress,
        };
        return payload;
    }

    [Fact]
    public async Task Store_WhenPointsLackFingerprint_ThrowsTypedErrorOnFirstUse()
    {
        var db = new FakeQdrantVectorDb();
        var orphan = TestMemories.Create("orphan");
        db.Seed(orphan.Id, new float[4], MemoryPayloadMapper.ToPayload(orphan));
        var store = NewStore(db);

        var exception = await Should.ThrowAsync<EmbeddingFingerprintException>(
            () => store.ListAsync(new MemoryFilter(), CancellationToken.None));

        exception.Message.ShouldContain("fingerprint");
    }

    [Fact]
    public async Task Store_WithFingerprintMarkerPresent_ReadsExcludeMarkerPoint()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var active = await SeedAsync(store, TestMemories.Create("active"), 1, 0, 0, 0);
        await SeedAsync(store, TestMemories.Create("superseded", status: MemoryStatus.Superseded, supersededBy: active.Id));
        await SeedAsync(store, TestMemories.Create("tombstoned", status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: TestMemories.Moment));

        db.PointIds.ShouldContain(EmbeddingFingerprint.MarkerPointId);

        var records = await store.ListAsync(new MemoryFilter(), CancellationToken.None);
        var points = await store.FetchAsync(new MemoryFilter(), CancellationToken.None);
        var matches = await store.SearchAsync(new float[] { 1, 0, 0, 0 }, new MemoryFilter(), 10, CancellationToken.None);
        var count = await store.CountAsync(new MemoryFilter(), CancellationToken.None);

        records.Select(record => record.Id).ShouldBe([active.Id]);
        points.Select(point => point.Record.Id).ShouldBe([active.Id]);
        matches.Select(match => match.Record.Id).ShouldBe([active.Id]);
        count.ShouldBe(1);
    }

    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.Unauthenticated)]
    public async Task Store_WhenQdrantFailsWithAvailabilityGrade_ThrowsUpstreamUnavailable(StatusCode statusCode)
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        await store.CountAsync(new MemoryFilter(), CancellationToken.None);
        db.Failure = new RpcException(new Status(statusCode, "upstream degraded"));

        var exception = await Should.ThrowAsync<UpstreamUnavailableException>(
            () => store.CountAsync(new MemoryFilter(), CancellationToken.None));

        exception.Upstream.ShouldBe("qdrant");
        exception.InnerException.ShouldBeOfType<RpcException>();
        exception.InnerException!.Message.ShouldContain("upstream degraded");
    }

    [Fact]
    public async Task Store_WhenQdrantRejectsPermissions_KeepsRawRpcException()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        await store.CountAsync(new MemoryFilter(), CancellationToken.None);
        db.Failure = new RpcException(new Status(StatusCode.PermissionDenied, "forbidden"));

        await Should.ThrowAsync<RpcException>(
            () => store.CountAsync(new MemoryFilter(), CancellationToken.None));
    }

    [Fact]
    public async Task Store_WhenBootstrapCannotReachQdrant_ThrowsUpstreamUnavailable()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        db.Failure = new RpcException(new Status(StatusCode.Unavailable, "host unreachable"));

        var exception = await Should.ThrowAsync<UpstreamUnavailableException>(
            () => store.GetAsync(Guid.NewGuid(), CancellationToken.None));

        exception.Upstream.ShouldBe("qdrant");
        exception.InnerException!.Message.ShouldContain("host unreachable");
    }

    [Fact]
    public async Task UpsertBatch_WritesVectorsAndModelMarker_InChunksOf256()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        await store.CountAsync(new MemoryFilter(), CancellationToken.None);
        var upsertsBeforeBatch = db.UpsertCount;
        var points = Enumerable.Range(0, 300)
            .Select(index => new MemoryPoint
            {
                Record = TestMemories.Create($"batch-{index}", id: BatchId(index)),
                Vector = new float[] { index, 1, 0, 0 },
            })
            .ToList();

        await store.UpsertBatchAsync(points, CancellationToken.None);

        (db.UpsertCount - upsertsBeforeBatch).ShouldBe(2);
        foreach (var point in points)
        {
            db.VectorOf(point.Record.Id).ShouldBe(point.Vector.ToArray());
            db.PayloadOf(point.Record.Id)[MemoryPayloadFields.VectorModel].StringValue.ShouldBe("nomic-embed-text");
        }
    }

    [Fact]
    public async Task EnumerateAll_CoversEveryStatusWithVectors_AndSkipsTheMarkerPoint()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var active = await SeedAsync(store, TestMemories.Create("active"), 1, 0, 0, 0);
        var superseded = await SeedAsync(
            store,
            TestMemories.Create("superseded", status: MemoryStatus.Superseded, supersededBy: active.Id),
            0, 1, 0, 0);
        var tombstoned = await SeedAsync(
            store,
            TestMemories.Create("gone", status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: TestMemories.Moment),
            0, 0, 1, 0);

        var enumerated = await EnumerateAllAsync(store);

        enumerated.Select(point => point.Record.Id).ShouldBe([active.Id, superseded.Id, tombstoned.Id]);
        enumerated.ShouldNotContain(point => point.Record.Id == EmbeddingFingerprint.MarkerPointId);
        enumerated.Single(point => point.Record.Id == tombstoned.Id).Vector.ToArray().ShouldBe([0f, 0, 1, 0]);
    }

    [Fact]
    public async Task EnumerateAll_PagesThroughStoresLargerThanOneScrollPage()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);
        var ids = new List<Guid>();

        for (var index = 0; index < PointsPerScrollPage + 1; index++)
        {
            ids.Add((await SeedAsync(store, TestMemories.Create($"bulk-{index}"), 1, 0, 0, 0)).Id);
        }

        var enumerated = await EnumerateAllAsync(store);

        enumerated.Select(point => point.Record.Id).ShouldBe(ids);
    }

    private static async Task<IReadOnlyList<MemoryPoint>> EnumerateAllAsync(QdrantMemoryStore store)
    {
        var enumerated = new List<MemoryPoint>();

        await foreach (var point in store.EnumerateAllAsync(CancellationToken.None))
        {
            enumerated.Add(point);
        }

        return enumerated;
    }

    private static Guid BatchId(int index) => new(index, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static QdrantMemoryStore NewStore(FakeQdrantVectorDb db)
    {
        db.CollectionExists = true;
        return new(db, "grimoire", new EmbeddingOptions { Model = "nomic-embed-text", Dimensions = 4 });
    }

    private static async Task<MemoryRecord> SeedAsync(QdrantMemoryStore store, MemoryRecord record, params float[] vector)
    {
        await store.UpsertAsync(record, vector, CancellationToken.None);
        return record;
    }

    private static async Task<IReadOnlyList<Guid>> ListIdsAsync(IMemoryStore store, MemoryFilter filter)
    {
        var records = await store.ListAsync(filter, CancellationToken.None);
        return [.. records.Select(record => record.Id)];
    }
}
