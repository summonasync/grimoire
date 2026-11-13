using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Sessions;
using Grimoire.Infrastructure.Tests.Fakes;
using Grpc.Core;
using Qdrant.Client.Grpc;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Sessions;

public sealed class QdrantSessionStoreTests
{
    private static readonly DateTimeOffset Moment = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeQdrantVectorDb _db = new();

    private readonly QdrantSessionStore _store;

    public QdrantSessionStoreTests()
    {
        _db.CollectionExists = true;
        _store = new QdrantSessionStore(_db, new QdrantOptions { Collection = "grimoire" });
    }

    [Fact]
    public async Task Boot_CreatesMissingCollectionWithoutVectorsAndNoEmbedder()
    {
        var db = new FakeQdrantVectorDb { CollectionExists = false };
        var store = new QdrantSessionStore(db, new QdrantOptions { Collection = "grimoire" });

        await store.WriteAsync(Entry(), CancellationToken.None);

        db.CreatedWithoutVectors.ShouldBeTrue();
        db.CreatedDimensions.ShouldBeNull();
        db.CreatedIndexes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Boot_DoesNotRecreateAnExistingCollection()
    {
        await _store.WriteAsync(Entry(), CancellationToken.None);

        _db.CreateCollectionCount.ShouldBe(0);
    }

    [Fact]
    public async Task Write_TargetsTheSessionsCollectionDerivedFromTheMemoryCollection()
    {
        await _store.WriteAsync(Entry(), CancellationToken.None);

        _db.UsedCollections.ShouldContain("grimoire-sessions");
    }

    [Fact]
    public async Task Write_WithConfiguredSessionCollection_TargetsTheOverride()
    {
        var db = new FakeQdrantVectorDb { CollectionExists = true };
        var store = new QdrantSessionStore(db, new QdrantOptions { Collection = "grimoire", SessionCollection = "custom-sessions" });

        await store.WriteAsync(Entry(), CancellationToken.None);

        db.UsedCollections.ShouldContain("custom-sessions");
    }

    [Fact]
    public async Task Write_RoundTripsEveryContractField()
    {
        var entry = Entry();

        await _store.WriteAsync(entry, CancellationToken.None);
        var loaded = await _store.ListRecentAsync(10, null, CancellationToken.None);

        loaded.ShouldHaveSingleItem().ShouldBe(entry);
    }

    [Fact]
    public async Task Write_AllOptionalFieldsNull_DecodesAsNullNotPlaceholders()
    {
        var bare = new SessionRegistryEntry
        {
            OccurredAt = Moment,
            Transport = "stdio",
            Host = "box-01",
        };

        await _store.WriteAsync(bare, CancellationToken.None);
        var loaded = await _store.ListRecentAsync(10, null, CancellationToken.None);

        var entry = loaded.ShouldHaveSingleItem();
        entry.ProtocolVersion.ShouldBeNull();
        entry.ClientName.ShouldBeNull();
        entry.ClientVersion.ShouldBeNull();
        entry.ActorAlias.ShouldBeNull();
        entry.ActorVendor.ShouldBeNull();
        entry.SessionId.ShouldBeNull();
        entry.SchemaVersion.ShouldBe(SessionRegistryEntry.CurrentSchemaVersion);
    }

    [Fact]
    public async Task Write_IdenticalHandshakesTwice_StoresTwoPointsBecauseOneRowIsOneObservation()
    {
        await _store.WriteAsync(Entry(), CancellationToken.None);
        await _store.WriteAsync(Entry(), CancellationToken.None);

        _db.PointIds.Count.ShouldBe(2);
        (await _store.ListRecentAsync(10, null, CancellationToken.None)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListRecent_OrdersNewestFirst()
    {
        await _store.WriteAsync(Entry(occurredAt: Moment), CancellationToken.None);
        await _store.WriteAsync(Entry(occurredAt: Moment.AddHours(2)), CancellationToken.None);
        await _store.WriteAsync(Entry(occurredAt: Moment.AddHours(1)), CancellationToken.None);

        var loaded = await _store.ListRecentAsync(10, null, CancellationToken.None);

        loaded.Select(entry => entry.OccurredAt).ShouldBe(
            [Moment.AddHours(2), Moment.AddHours(1), Moment]);
    }

    [Fact]
    public async Task ListRecent_AppliesLimitThenWindow()
    {
        await _store.WriteAsync(Entry(occurredAt: Moment.AddHours(3)), CancellationToken.None);
        await _store.WriteAsync(Entry(occurredAt: Moment.AddHours(2)), CancellationToken.None);
        await _store.WriteAsync(Entry(occurredAt: Moment.AddHours(1)), CancellationToken.None);

        var loaded = await _store.ListRecentAsync(2, Moment.AddHours(1), CancellationToken.None);

        loaded.Select(entry => entry.OccurredAt).ShouldBe([Moment.AddHours(3), Moment.AddHours(2)]);
    }

    [Fact]
    public async Task ListRecent_WindowBoundaryIsInclusive()
    {
        await _store.WriteAsync(Entry(occurredAt: Moment), CancellationToken.None);

        var loaded = await _store.ListRecentAsync(10, Moment, CancellationToken.None);

        loaded.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Prune_DeletesRowsStrictlyOlderThanCutoffAndKeepsTheRest()
    {
        var older = Entry(occurredAt: Moment.AddHours(-1));
        await _store.WriteAsync(older, CancellationToken.None);
        await _store.WriteAsync(Entry(occurredAt: Moment), CancellationToken.None);

        var removed = await _store.PruneAsync(Moment, CancellationToken.None);

        removed.ShouldBe(1);
        var remaining = await _store.ListRecentAsync(10, null, CancellationToken.None);
        remaining.ShouldHaveSingleItem().OccurredAt.ShouldBe(Moment);
    }

    [Fact]
    public async Task Prune_NothingExpired_DeletesNothing()
    {
        await _store.WriteAsync(Entry(), CancellationToken.None);

        var removed = await _store.PruneAsync(Moment.AddDays(-1), CancellationToken.None);

        removed.ShouldBe(0);
        _db.DeletedBatches.ShouldBeEmpty();
        _db.PointIds.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.Unauthenticated)]
    public async Task ListRecent_WhenQdrantIsUnavailable_DegradesToEmptyWithAnHonestReason(StatusCode statusCode)
    {
        _db.Failure = new RpcException(new Status(statusCode, "host unreachable"));

        var loaded = await _store.ListRecentAsync(10, null, CancellationToken.None);

        loaded.ShouldBeEmpty();
        _store.DegradationReason.ShouldNotBeNull();
        _store.DegradationReason.ShouldContain("unreachable");
    }

    [Fact]
    public async Task ListRecent_AfterDegradation_ClearsTheReasonOnRecovery()
    {
        _db.Failure = new RpcException(new Status(StatusCode.Unavailable, "host unreachable"));
        await _store.ListRecentAsync(10, null, CancellationToken.None);
        _store.DegradationReason.ShouldNotBeNull();
        _db.Failure = null;

        await _store.ListRecentAsync(10, null, CancellationToken.None);

        _store.DegradationReason.ShouldBeNull();
    }

    [Fact]
    public async Task Write_WhenQdrantIsUnavailable_ThrowsUpstreamUnavailable()
    {
        _db.Failure = new RpcException(new Status(StatusCode.Unavailable, "host unreachable"));

        var exception = await Should.ThrowAsync<UpstreamUnavailableException>(
            () => _store.WriteAsync(Entry(), CancellationToken.None));

        exception.Upstream.ShouldBe("qdrant");
    }

    [Fact]
    public async Task Write_WhenCollectionDropsMidSession_ResetsBootAndRecoversOnTheNextRead()
    {
        await _store.ListRecentAsync(10, null, CancellationToken.None);
        _db.UpsertFailure = new RpcException(new Status(StatusCode.NotFound, "collection not found"));

        var exception = await Should.ThrowAsync<UpstreamUnavailableException>(
            () => _store.WriteAsync(Entry(), CancellationToken.None));

        exception.InnerException.ShouldNotBeNull().Message.ShouldContain("collection not found");

        _db.UpsertFailure = null;
        (await _store.ListRecentAsync(10, null, CancellationToken.None)).ShouldBeEmpty();
        _store.DegradationReason.ShouldBeNull();
    }

    private static SessionRegistryEntry Entry(
        DateTimeOffset? occurredAt = null,
        string? clientName = "qwen",
        string? protocolVersion = "2025-06-18") => new()
        {
            OccurredAt = occurredAt ?? Moment,
            ClientName = clientName,
            ClientVersion = "0.17",
            ProtocolVersion = protocolVersion,
            ActorAlias = "guldana",
            ActorVendor = "qwen",
            Transport = "stdio",
            Host = "box-01",
            SessionId = "sess-1",
        };
}
