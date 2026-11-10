using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Grimoire.Infrastructure.Tests.Fakes;
using Grimoire.Infrastructure.Tomes;
using Grpc.Core;
using Qdrant.Client.Grpc;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Tomes;

public sealed class QdrantManifestStoreTests
{
    private static readonly DateTimeOffset Moment = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeQdrantVectorDb _db = new();

    private readonly QdrantManifestStore _store;

    public QdrantManifestStoreTests()
    {
        _db.CollectionExists = true;
        _store = new QdrantManifestStore(_db, new QdrantOptions { Collection = "grimoire" });
    }

    [Fact]
    public async Task Create_WritesPayloadOnlyPointUnderTheDerivedCollection()
    {
        var manifest = Manifest("dev");

        await _store.CreateAsync(manifest, CancellationToken.None);

        var id = TomeManifestKeyId.For("dev");
        _db.PointIds.ShouldHaveSingleItem().ShouldBe(id);
        _db.VectorOf(id).ShouldBeEmpty();
        _db.PayloadOf(id)[ManifestPayloadFields.Key].StringValue.ShouldBe("dev");
        _db.PayloadOf(id)[ManifestPayloadFields.Description].StringValue.ShouldBe("Project work");
        _db.PayloadOf(id)[ManifestPayloadFields.CreatedBy].StringValue.ShouldBe("human");
        _db.PayloadOf(id)[ManifestPayloadFields.CreatedAt].StringValue.ShouldBe(Moment.ToString("O"));
        _db.PayloadOf(id)[ManifestPayloadFields.UpdatedAt].StringValue.ShouldBe(Moment.ToString("O"));
    }

    [Fact]
    public async Task Create_UsesTheConfiguredManifestCollectionOverride()
    {
        var db = new FakeQdrantVectorDb { CollectionExists = true };
        var store = new QdrantManifestStore(
            db,
            new QdrantOptions { Collection = "grimoire", ManifestCollection = "custom-manifests" });

        await store.CreateAsync(Manifest("dev"), CancellationToken.None);

        db.PointIds.ShouldHaveSingleItem().ShouldBe(TomeManifestKeyId.For("dev"));
    }

    [Fact]
    public async Task Create_IsIdempotentPerKey_BecausePointIdsAreDeterministic()
    {
        await _store.CreateAsync(Manifest("dev"), CancellationToken.None);
        await _store.CreateAsync(Manifest("dev") with { Description = "Rewritten" }, CancellationToken.None);

        _db.PointIds.ShouldHaveSingleItem();
        _db.PayloadOf(TomeManifestKeyId.For("dev"))[ManifestPayloadFields.Description].StringValue.ShouldBe("Rewritten");
    }

    [Fact]
    public async Task Get_RoundTripsEveryManifestField()
    {
        await _store.CreateAsync(Manifest("dev"), CancellationToken.None);

        var loaded = await _store.GetAsync("dev", CancellationToken.None);

        var manifest = loaded.ShouldNotBeNull();
        manifest.Key.ShouldBe("dev");
        manifest.Description.ShouldBe("Project work");
        manifest.CreatedBy.ShouldBe("human");
        manifest.CreatedAt.ShouldBe(Moment);
        manifest.UpdatedAt.ShouldBe(Moment);
        (await _store.GetAsync("ghost", CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task Update_OverwritesDescriptionAndTimestampsAndKeepsCreatorProvenance()
    {
        await _store.CreateAsync(Manifest("dev"), CancellationToken.None);
        var later = Moment.AddHours(3);

        await _store.UpdateAsync("dev", "Domain work", MemoryActor.Agent("coder", "claude"), later, CancellationToken.None);

        var payload = _db.PayloadOf(TomeManifestKeyId.For("dev"));
        payload[ManifestPayloadFields.Description].StringValue.ShouldBe("Domain work");
        payload[ManifestPayloadFields.UpdatedAt].StringValue.ShouldBe(later.ToString("O"));
        payload[ManifestPayloadFields.UpdatedBy].StringValue.ShouldBe("coder+claude");
        payload[ManifestPayloadFields.CreatedBy].StringValue.ShouldBe("human");
        payload[ManifestPayloadFields.CreatedAt].StringValue.ShouldBe(Moment.ToString("O"));

        var loaded = await _store.GetAsync("dev", CancellationToken.None);
        loaded!.Description.ShouldBe("Domain work");
        loaded.UpdatedAt.ShouldBe(later);
        loaded.CreatedAt.ShouldBe(Moment);
        loaded.CreatedBy.ShouldBe("human");
    }

    [Fact]
    public async Task Delete_RemovesTheManifestPoint()
    {
        await _store.CreateAsync(Manifest("dev"), CancellationToken.None);

        await _store.DeleteAsync("dev", CancellationToken.None);

        _db.PointIds.ShouldBeEmpty();
        (await _store.GetAsync("dev", CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task List_ReturnsEveryManifest()
    {
        await _store.CreateAsync(Manifest("dev"), CancellationToken.None);
        await _store.CreateAsync(Manifest("env", "Environment and infra"), CancellationToken.None);

        var manifests = await _store.ListAsync(CancellationToken.None);

        manifests.Select(manifest => manifest.Key).ShouldBe(["dev", "env"]);
    }

    [Fact]
    public async Task Boot_CreatesMissingCollectionWithoutVectorsAndIndexesTheKey()
    {
        var db = new FakeQdrantVectorDb { CollectionExists = false };
        var store = new QdrantManifestStore(db, new QdrantOptions { Collection = "grimoire" });

        var manifests = await store.ListAsync(CancellationToken.None);

        manifests.ShouldBeEmpty();
        db.CreatedWithoutVectors.ShouldBeTrue();
        db.CreatedDimensions.ShouldBeNull();
        db.CreatedIndexes.ShouldHaveSingleItem().ShouldBe((ManifestPayloadFields.Key, PayloadSchemaType.Keyword));
    }

    [Fact]
    public async Task Boot_WithExistingCollection_DoesNotRecreateButBackfillsMissingIndex()
    {
        var db = new FakeQdrantVectorDb { CollectionExists = true };
        var store = new QdrantManifestStore(db, new QdrantOptions { Collection = "grimoire" });

        await store.ListAsync(CancellationToken.None);

        db.CreateCollectionCount.ShouldBe(0);
        db.CreatedIndexes.ShouldHaveSingleItem().ShouldBe((ManifestPayloadFields.Key, PayloadSchemaType.Keyword));
    }

    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.Unauthenticated)]
    public async Task Boot_WhenQdrantIsUnavailable_DegradesReadsWithAnHonestReason(StatusCode statusCode)
    {
        _db.Failure = new RpcException(new Status(statusCode, "host unreachable"));

        var loaded = await _store.GetAsync("dev", CancellationToken.None);
        var manifests = await _store.ListAsync(CancellationToken.None);

        loaded.ShouldBeNull();
        manifests.ShouldBeEmpty();
        _store.DegradationReason.ShouldNotBeNull();
        _store.DegradationReason.ShouldContain("unreachable");
    }

    [Fact]
    public async Task Read_AfterDegradation_ClearsTheReasonOnRecovery()
    {
        _db.Failure = new RpcException(new Status(StatusCode.Unavailable, "host unreachable"));
        await _store.ListAsync(CancellationToken.None);
        _store.DegradationReason.ShouldNotBeNull();
        _db.Failure = null;

        await _store.ListAsync(CancellationToken.None);

        _store.DegradationReason.ShouldBeNull();
    }

    [Fact]
    public async Task Write_WhenQdrantIsUnavailable_ThrowsUpstreamUnavailable()
    {
        _db.Failure = new RpcException(new Status(StatusCode.Unavailable, "host unreachable"));

        var exception = await Should.ThrowAsync<UpstreamUnavailableException>(
            () => _store.CreateAsync(Manifest("dev"), CancellationToken.None));

        exception.Upstream.ShouldBe("qdrant");
    }

    [Fact]
    public async Task Write_WhenCollectionDropsMidSession_ResetsBootAndRecoversOnTheNextRead()
    {
        await _store.ListAsync(CancellationToken.None);
        _db.UpsertFailure = new RpcException(new Status(StatusCode.NotFound, "collection not found"));

        var exception = await Should.ThrowAsync<UpstreamUnavailableException>(
            () => _store.CreateAsync(Manifest("dev"), CancellationToken.None));

        exception.InnerException.ShouldNotBeNull().Message.ShouldContain("collection not found");

        _db.UpsertFailure = null;
        (await _store.ListAsync(CancellationToken.None)).ShouldBeEmpty();
    }

    private static TomeManifest Manifest(string key, string description = "Project work") => new()
    {
        Key = key,
        Description = description,
        CreatedBy = "human",
        CreatedAt = Moment,
        UpdatedAt = Moment,
    };
}
