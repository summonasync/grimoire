using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Grimoire.Infrastructure.Tests.Fakes;
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
            sources: [source]);
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
    public async Task Store_WhenCollectionMissing_BootstrapsCollectionAndIndexesOnFirstUse()
    {
        var db = new FakeQdrantVectorDb();
        var store = NewStore(db);

        var records = await store.ListAsync(new MemoryFilter(), CancellationToken.None);

        records.ShouldBeEmpty();
        db.CreateCollectionCount.ShouldBe(1);
        db.CreatedDimensions.ShouldBe(4);
        db.CreatedIndexes.Select(index => index.Field).ShouldBe(
            ["scope", "kind", "status", "tags", "projectId", "sessionId", "expiresAt"], ignoreOrder: true);
        db.CreatedIndexes.ShouldContain(index => index.Type == PayloadSchemaType.Datetime);
        db.CreatedIndexes.Count(index => index.Type == PayloadSchemaType.Keyword).ShouldBe(6);
        db.PointIds.ShouldContain(EmbeddingFingerprint.MarkerPointId);
    }

    [Fact]
    public async Task Store_WhenEmbeddingModelDiffers_ThrowsTypedErrorOnFirstUse()
    {
        var db = new FakeQdrantVectorDb();
        db.Seed(
            EmbeddingFingerprint.MarkerPointId,
            new float[4],
            EmbeddingFingerprint.From("other-model", 4).ToPayload());
        var store = NewStore(db);

        var exception = await Should.ThrowAsync<EmbeddingFingerprintException>(
            () => store.ListAsync(new MemoryFilter(), CancellationToken.None));

        exception.Message.ShouldContain("other-model");
    }

    [Fact]
    public async Task Store_WhenEmbeddingDimensionsDiffers_ThrowsTypedErrorOnFirstUse()
    {
        var db = new FakeQdrantVectorDb();
        db.Seed(
            EmbeddingFingerprint.MarkerPointId,
            new float[8],
            EmbeddingFingerprint.From("nomic-embed-text", 8).ToPayload());
        var store = NewStore(db);

        await Should.ThrowAsync<EmbeddingFingerprintException>(
            () => store.ListAsync(new MemoryFilter(), CancellationToken.None));
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

    private static QdrantMemoryStore NewStore(FakeQdrantVectorDb db) =>
        new(db, "grimoire", new EmbeddingOptions { Model = "nomic-embed-text", Dimensions = 4 });

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
