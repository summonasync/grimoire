using Grimoire.Core;
using Qdrant.Client.Grpc;

namespace Grimoire.Infrastructure.Memory;

/// <summary>
/// Store-wide re-embedding sweep behind `grimoire reindex`. Commits the target fingerprint
/// with the reindex-in-progress marker BEFORE the sweep, so every face refuses operations
/// until completion; a crash mid-sweep therefore leaves the store in the refuses-ops state
/// and the next reindex run resumes it. Records carrying the vectorModel payload marker
/// of the target model are skipped on re-runs; every store upsert stamps that marker, so
/// records written between reindexes are skipped too. Tombstoned and superseded records
/// are never re-embedded; the sweep covers all tomes because it is an ops command.
/// </summary>
public sealed class EmbeddingReindexer(
    IQdrantVectorDb db,
    string collection,
    QdrantCollectionBootstrapper bootstrapper,
    IEmbeddingProvider embeddings,
    int batchSize)
{
    private readonly int _batchSize = batchSize > 0
        ? batchSize
        : throw new ArgumentOutOfRangeException(nameof(batchSize));

    public async Task<ReindexOutcome> RunAsync(
        EmbeddingFingerprint target,
        Action<ReindexProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!await db.CollectionExistsAsync(collection, cancellationToken))
        {
            await bootstrapper.EnsureAsync(target, cancellationToken);
            return new ReindexOutcome(0, 0, 0);
        }

        await bootstrapper.BeginReindexAsync(target, cancellationToken);

        var filter = MemoryFilterTranslator.Translate(new MemoryFilter { Status = MemoryStatus.Active });
        var total = await db.CountAsync(collection, filter, cancellationToken);
        var processed = 0;
        var reembedded = 0;
        var skipped = 0;
        PointId? offset = null;

        do
        {
            var page = await db.ScrollAsync(collection, filter, (uint)_batchSize, offset, withVectors: false, cancellationToken);
            var upserts = new List<PointStruct>();

            foreach (var point in page.Result)
            {
                processed++;

                if (CarriesTargetVector(point.Payload, target))
                {
                    skipped++;
                    continue;
                }

                upserts.Add(ReembeddedPoint(point, await EmbedAsync(point, cancellationToken), target));
                reembedded++;
            }

            if (upserts.Count > 0)
            {
                await db.UpsertAsync(collection, upserts, cancellationToken);
            }

            progress?.Invoke(new ReindexProgress(processed, (int)total));
            offset = page.NextPageOffset;
        }
        while (offset is not null);

        await bootstrapper.CompleteReindexAsync(target, cancellationToken);
        return new ReindexOutcome(processed, reembedded, skipped);
    }

    private static bool CarriesTargetVector(IReadOnlyDictionary<string, Value> payload, EmbeddingFingerprint target) =>
        payload.TryGetValue(MemoryPayloadFields.VectorModel, out var model)
        && model.KindCase == Value.KindOneofCase.StringValue
        && model.StringValue == target.Model;

    private async Task<ReadOnlyMemory<float>> EmbedAsync(RetrievedPoint point, CancellationToken cancellationToken)
    {
        if (!point.Payload.TryGetValue(MemoryPayloadFields.Content, out var content)
            || content.KindCase != Value.KindOneofCase.StringValue)
        {
            throw new PayloadDecodeException(
                $"Memory point '{point.Id.Uuid ?? point.Id.ToString()}' does not carry embeddable content; fix or delete the point before resuming the reindex.");
        }

        return await embeddings.EmbedAsync(content.StringValue, cancellationToken);
    }

    private static PointStruct ReembeddedPoint(RetrievedPoint source, ReadOnlyMemory<float> vector, EmbeddingFingerprint target)
    {
        var point = new PointStruct { Id = source.Id, Vectors = vector.ToArray() };

        foreach (var pair in source.Payload)
        {
            point.Payload.Add(pair.Key, pair.Value);
        }

        point.Payload[MemoryPayloadFields.VectorModel] = target.Model;
        return point;
    }
}
