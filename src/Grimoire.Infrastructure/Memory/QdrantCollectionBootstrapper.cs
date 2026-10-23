using Grimoire.Core;
using Qdrant.Client.Grpc;

namespace Grimoire.Infrastructure.Memory;

public sealed class QdrantCollectionBootstrapper(IQdrantVectorDb db, string collection)
{
    private static readonly (string Field, PayloadSchemaType Type)[] Indexes =
    [
        (MemoryPayloadFields.Scope, PayloadSchemaType.Keyword),
        (MemoryPayloadFields.Kind, PayloadSchemaType.Keyword),
        (MemoryPayloadFields.Status, PayloadSchemaType.Keyword),
        (MemoryPayloadFields.Tome, PayloadSchemaType.Keyword),
        (MemoryPayloadFields.Tags, PayloadSchemaType.Keyword),
        (MemoryPayloadFields.ProjectId, PayloadSchemaType.Keyword),
        (MemoryPayloadFields.SessionId, PayloadSchemaType.Keyword),
        (MemoryPayloadFields.ExpiresAt, PayloadSchemaType.Datetime),
        (MemoryPayloadFields.CreatedAt, PayloadSchemaType.Datetime),
        (MemoryPayloadFields.IdempotencyKey, PayloadSchemaType.Keyword),
    ];

    public async Task EnsureAsync(EmbeddingFingerprint expected, CancellationToken cancellationToken)
    {
        if (!await db.CollectionExistsAsync(collection, cancellationToken))
        {
            await db.CreateCollectionAsync(collection, expected.Dimensions, cancellationToken);
            await CreateIndexesAsync([], cancellationToken);
            await WriteMarkerAsync(expected, reindexInProgress: false, cancellationToken);
            return;
        }

        await VerifyFingerprintAsync(expected, cancellationToken);
    }

    public async Task EnsurePresentAsync(EmbeddingFingerprint expected, CancellationToken cancellationToken)
    {
        if (!await db.CollectionExistsAsync(collection, cancellationToken))
        {
            throw new ReindexRequiredException(
                $"Collection '{collection}' is missing — it was dropped or never initialized. " +
                "Run `grimoire init` to create it or `grimoire reindex` to rebuild it; operations stay refused until then.");
        }

        await VerifyFingerprintAsync(expected, cancellationToken);
    }

    private async Task VerifyFingerprintAsync(EmbeddingFingerprint expected, CancellationToken cancellationToken)
    {
        var info = await db.GetCollectionInfoAsync(collection, cancellationToken);
        await CreateIndexesAsync(info.PayloadSchema.Keys, cancellationToken);

        var (stored, reindexInProgress) = await ReadMarkerAsync(cancellationToken);

        if (reindexInProgress && stored is { } running)
        {
            throw new ReindexRequiredException(
                $"An embedding reindex to '{running.Model}' is in progress — run `grimoire reindex` to resume it. " +
                "Operations stay refused until the sweep finishes.");
        }

        if (stored is null)
        {
            if (info.HasPointsCount && info.PointsCount > 0)
            {
                throw new EmbeddingFingerprintException(
                    $"Collection '{collection}' holds {info.PointsCount} points without an embedding fingerprint; refusing to guess the embedding model.");
            }

            await WriteMarkerAsync(expected, reindexInProgress: false, cancellationToken);
            return;
        }

        if (!stored.Value.Equals(expected))
        {
            throw new ReindexRequiredException(
                $"Embedding model changed from '{stored.Value.Model}' ({stored.Value.Dimensions} dimensions) " +
                $"to '{expected.Model}' ({expected.Dimensions} dimensions) — run `grimoire reindex`. " +
                "Operations stay refused until the store is rebuilt for the configured model.");
        }
    }

    public async Task BeginReindexAsync(EmbeddingFingerprint target, CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(cancellationToken);
        await WriteMarkerAsync(target, reindexInProgress: true, cancellationToken);
    }

    public Task CompleteReindexAsync(EmbeddingFingerprint target, CancellationToken cancellationToken) =>
        WriteMarkerAsync(target, reindexInProgress: false, cancellationToken);

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var info = await db.GetCollectionInfoAsync(collection, cancellationToken);
        await CreateIndexesAsync(info.PayloadSchema.Keys, cancellationToken);
    }

    private async Task<(EmbeddingFingerprint? Fingerprint, bool ReindexInProgress)> ReadMarkerAsync(CancellationToken cancellationToken)
    {
        var points = await db.RetrieveAsync(collection, [EmbeddingFingerprint.MarkerPointId], withVectors: false, cancellationToken);

        if (points.Count == 0)
        {
            return (null, false);
        }

        var payload = points[0].Payload;
        var fingerprint = EmbeddingFingerprint.FromPayload(payload)
            ?? throw new EmbeddingFingerprintException($"The embedding fingerprint marker in '{collection}' is incomplete.");
        var inProgress = payload.TryGetValue(EmbeddingFingerprint.ReindexStateField, out var state)
            && state.KindCase == Value.KindOneofCase.StringValue
            && state.StringValue == EmbeddingFingerprint.ReindexStateInProgress;

        return (fingerprint, inProgress);
    }

    private async Task CreateIndexesAsync(IEnumerable<string> existing, CancellationToken cancellationToken)
    {
        var present = existing.ToHashSet(StringComparer.Ordinal);

        foreach (var (field, type) in Indexes)
        {
            if (!present.Contains(field))
            {
                await db.CreatePayloadIndexAsync(collection, field, type, cancellationToken);
            }
        }
    }

    private async Task WriteMarkerAsync(EmbeddingFingerprint fingerprint, bool reindexInProgress, CancellationToken cancellationToken)
    {
        var marker = new PointStruct { Id = EmbeddingFingerprint.MarkerPointId, Vectors = new float[fingerprint.Dimensions] };

        foreach (var pair in fingerprint.ToPayload())
        {
            marker.Payload.Add(pair.Key, pair.Value);
        }

        if (reindexInProgress)
        {
            marker.Payload[EmbeddingFingerprint.ReindexStateField] = EmbeddingFingerprint.ReindexStateInProgress;
        }

        await db.UpsertAsync(collection, [marker], cancellationToken);
    }
}
