using Qdrant.Client.Grpc;

namespace Grimoire.Infrastructure.Memory;

public sealed class QdrantCollectionBootstrapper(IQdrantVectorDb db, string collection)
{
    private static readonly (string Field, PayloadSchemaType Type)[] Indexes =
    [
        (MemoryPayloadFields.Scope, PayloadSchemaType.Keyword),
        (MemoryPayloadFields.Kind, PayloadSchemaType.Keyword),
        (MemoryPayloadFields.Status, PayloadSchemaType.Keyword),
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
            await WriteFingerprintAsync(expected, cancellationToken);
            return;
        }

        var info = await db.GetCollectionInfoAsync(collection, cancellationToken);
        await CreateIndexesAsync(info.PayloadSchema.Keys, cancellationToken);

        var stored = await ReadFingerprintAsync(cancellationToken);
        if (stored is null)
        {
            if (info.HasPointsCount && info.PointsCount > 0)
            {
                throw new EmbeddingFingerprintException(
                    $"Collection '{collection}' holds {info.PointsCount} points without an embedding fingerprint; refusing to guess the embedding model.");
            }

            await WriteFingerprintAsync(expected, cancellationToken);
            return;
        }

        if (!stored.Value.Equals(expected))
        {
            throw new EmbeddingFingerprintException(
                $"Collection '{collection}' was built with embedding model '{stored.Value.Model}' ({stored.Value.Dimensions} dimensions) but is used with '{expected.Model}' ({expected.Dimensions} dimensions).");
        }
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

    private async Task<EmbeddingFingerprint?> ReadFingerprintAsync(CancellationToken cancellationToken)
    {
        var points = await db.RetrieveAsync(collection, [EmbeddingFingerprint.MarkerPointId], withVectors: false, cancellationToken);

        return points.Count == 0
            ? null
            : EmbeddingFingerprint.FromPayload(points[0].Payload)
                ?? throw new EmbeddingFingerprintException($"The embedding fingerprint marker in '{collection}' is incomplete.");
    }

    private Task WriteFingerprintAsync(EmbeddingFingerprint expected, CancellationToken cancellationToken)
    {
        var marker = new PointStruct { Id = EmbeddingFingerprint.MarkerPointId, Vectors = new float[expected.Dimensions] };

        foreach (var pair in expected.ToPayload())
        {
            marker.Payload.Add(pair.Key, pair.Value);
        }

        return db.UpsertAsync(collection, [marker], cancellationToken);
    }
}
