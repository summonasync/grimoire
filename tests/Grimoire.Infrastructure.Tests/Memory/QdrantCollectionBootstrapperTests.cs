using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Grimoire.Infrastructure.Tests.Fakes;
using Qdrant.Client.Grpc;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Memory;

public sealed class QdrantCollectionBootstrapperTests
{
    [Fact]
    public async Task Ensure_WhenCollectionMissing_CreatesCollectionIndexesAndFingerprint()
    {
        var db = new FakeQdrantVectorDb();
        var bootstrapper = new QdrantCollectionBootstrapper(db, "grimoire");

        await bootstrapper.EnsureAsync(Fingerprint(), CancellationToken.None);

        db.CreateCollectionCount.ShouldBe(1);
        db.CreatedDimensions.ShouldBe(4);
        db.CreatedIndexes.Select(index => index.Field).ShouldBe(
            ["scope", "kind", "status", "tome", "tags", "projectId", "sessionId", "expiresAt", "createdAt", "idempotencyKey"],
            ignoreOrder: true);
        db.CreatedIndexes.Count(index => index.Type == PayloadSchemaType.Datetime).ShouldBe(2);
        db.CreatedIndexes.Count(index => index.Type == PayloadSchemaType.Keyword).ShouldBe(8);
        db.PointIds.ShouldContain(EmbeddingFingerprint.MarkerPointId);
        db.PayloadOf(EmbeddingFingerprint.MarkerPointId)[EmbeddingFingerprint.ModelField].StringValue
            .ShouldBe("nomic-embed-text");
        db.PayloadOf(EmbeddingFingerprint.MarkerPointId)[EmbeddingFingerprint.DimensionsField].IntegerValue
            .ShouldBe(4);
    }

    [Fact]
    public async Task Ensure_WhenCollectionExists_IsIdempotent()
    {
        var db = new FakeQdrantVectorDb();
        var bootstrapper = new QdrantCollectionBootstrapper(db, "grimoire");
        await bootstrapper.EnsureAsync(Fingerprint(), CancellationToken.None);
        var collectionCount = db.CreateCollectionCount;
        var indexCount = db.CreatedIndexes.Count;
        var upsertCount = db.UpsertCount;

        await bootstrapper.EnsureAsync(Fingerprint(), CancellationToken.None);

        db.CreateCollectionCount.ShouldBe(collectionCount);
        db.CreatedIndexes.Count.ShouldBe(indexCount);
        db.UpsertCount.ShouldBe(upsertCount);
    }

    [Fact]
    public async Task Ensure_WithDifferentModel_ThrowsReindexRequiredWithHint()
    {
        var db = new FakeQdrantVectorDb();
        var bootstrapper = new QdrantCollectionBootstrapper(db, "grimoire");
        await bootstrapper.EnsureAsync(Fingerprint(), CancellationToken.None);

        var exception = await Should.ThrowAsync<ReindexRequiredException>(
            () => bootstrapper.EnsureAsync(EmbeddingFingerprint.From("bge-small", 4), CancellationToken.None));

        exception.Message.ShouldContain("nomic-embed-text");
        exception.Message.ShouldContain("bge-small");
        exception.Message.ShouldContain("run `grimoire reindex`");
    }

    [Fact]
    public async Task Ensure_WithDifferentDimensions_ThrowsReindexRequired()
    {
        var db = new FakeQdrantVectorDb();
        var bootstrapper = new QdrantCollectionBootstrapper(db, "grimoire");
        await bootstrapper.EnsureAsync(Fingerprint(), CancellationToken.None);

        await Should.ThrowAsync<ReindexRequiredException>(
            () => bootstrapper.EnsureAsync(EmbeddingFingerprint.From("nomic-embed-text", 8), CancellationToken.None));
    }

    [Fact]
    public async Task Ensure_AfterBeginReindex_RefusesUntilCompleted()
    {
        var db = new FakeQdrantVectorDb();
        var bootstrapper = new QdrantCollectionBootstrapper(db, "grimoire");
        await bootstrapper.EnsureAsync(Fingerprint(), CancellationToken.None);
        var target = EmbeddingFingerprint.From("bge-small", 4);

        await bootstrapper.BeginReindexAsync(target, CancellationToken.None);

        var exception = await Should.ThrowAsync<ReindexRequiredException>(
            () => bootstrapper.EnsureAsync(target, CancellationToken.None));
        exception.Message.ShouldContain("in progress");
        exception.Message.ShouldContain("run `grimoire reindex`");

        await bootstrapper.CompleteReindexAsync(target, CancellationToken.None);
        await bootstrapper.EnsureAsync(target, CancellationToken.None);
    }

    [Fact]
    public async Task Ensure_WhenPointsLackFingerprint_ThrowsTypedError()
    {
        var db = new FakeQdrantVectorDb();
        var bootstrapper = new QdrantCollectionBootstrapper(db, "grimoire");
        var orphan = TestMemories.Create("orphan");
        db.Seed(orphan.Id, new float[4], MemoryPayloadMapper.ToPayload(orphan));

        var exception = await Should.ThrowAsync<EmbeddingFingerprintException>(
            () => bootstrapper.EnsureAsync(Fingerprint(), CancellationToken.None));

        exception.Message.ShouldContain("fingerprint");
    }

    [Fact]
    public async Task Ensure_WhenCollectionEmptyWithoutFingerprint_AdoptsCurrentFingerprint()
    {
        var db = new FakeQdrantVectorDb { CollectionExists = true };
        var bootstrapper = new QdrantCollectionBootstrapper(db, "grimoire");

        await bootstrapper.EnsureAsync(Fingerprint(), CancellationToken.None);

        db.PointIds.ShouldContain(EmbeddingFingerprint.MarkerPointId);
        db.PayloadOf(EmbeddingFingerprint.MarkerPointId)[EmbeddingFingerprint.ModelField].StringValue
            .ShouldBe("nomic-embed-text");
    }

    [Fact]
    public async Task Ensure_WithPartialIndexes_CreatesOnlyMissingIndexes()
    {
        var db = new FakeQdrantVectorDb { CollectionExists = true };
        var bootstrapper = new QdrantCollectionBootstrapper(db, "grimoire");
        await db.CreatePayloadIndexAsync("grimoire", "scope", PayloadSchemaType.Keyword, CancellationToken.None);
        await db.CreatePayloadIndexAsync("grimoire", "kind", PayloadSchemaType.Keyword, CancellationToken.None);
        var existing = db.CreatedIndexes.Count;

        await bootstrapper.EnsureAsync(Fingerprint(), CancellationToken.None);

        db.CreatedIndexes.Count.ShouldBe(existing + 8);
        db.CreatedIndexes.Skip(existing).Select(index => index.Field).ShouldBe(
            ["status", "tome", "tags", "projectId", "sessionId", "expiresAt", "createdAt", "idempotencyKey"], ignoreOrder: true);
    }

    [Fact]
    public async Task EnsurePresent_WhenCollectionMissing_ThrowsReindexRequiredWithoutCreating()
    {
        var db = new FakeQdrantVectorDb();
        var bootstrapper = new QdrantCollectionBootstrapper(db, "grimoire");

        var exception = await Should.ThrowAsync<ReindexRequiredException>(
            () => bootstrapper.EnsurePresentAsync(Fingerprint(), CancellationToken.None));

        db.CreateCollectionCount.ShouldBe(0);
        exception.Message.ShouldContain("missing");
        exception.Message.ShouldContain("`grimoire init`");
    }

    [Fact]
    public async Task EnsurePresent_WhenCollectionExistsWithCurrentFingerprint_Passes()
    {
        var db = new FakeQdrantVectorDb { CollectionExists = true };
        var bootstrapper = new QdrantCollectionBootstrapper(db, "grimoire");
        await bootstrapper.EnsureAsync(Fingerprint(), CancellationToken.None);

        await bootstrapper.EnsurePresentAsync(Fingerprint(), CancellationToken.None);

        db.PointIds.ShouldContain(EmbeddingFingerprint.MarkerPointId);
    }

    private static EmbeddingFingerprint Fingerprint() => EmbeddingFingerprint.From("nomic-embed-text", 4);
}
