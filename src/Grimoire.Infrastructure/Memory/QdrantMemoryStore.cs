using System.Runtime.CompilerServices;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grpc.Core;
using Qdrant.Client.Grpc;

namespace Grimoire.Infrastructure.Memory;

/// <summary>
/// Qdrant-backed <see cref="IMemoryStore"/>. Read contract: filtered reads
/// (list/fetch/search/count) always constrain status to active unless the filter names a
/// status explicitly, so tombstoned and superseded points never leak into unconstrained
/// queries; range filters are inclusive (ExpiresBefore/TombstonedBefore/CreatedAfter match
/// equal values); a null filter member is unconstrained; list/fetch results are ordered
/// createdAt desc then id asc with filter.Limit applied after ordering. GetAsync is a
/// raw point-by-id fetch that returns any status — Forget depends on seeing superseded
/// and tombstoned records. Scroll pagination is adapter-internal. EnumerateAllAsync is the
/// one status-unconstrained read: the ops dump for export and reindex-class sweeps, marker
/// point excluded, vectors included, paged the same way. UpsertBatchAsync writes record+vector
/// pairs as recorded (import path) in bounded chunks, stamping the configured vector-model
/// marker exactly like UpsertAsync. gRPC failures in the
/// availability grades (unavailable, deadline exceeded, unauthenticated) surface as
/// UpstreamUnavailableException; a NotFound RpcException (collection dropped mid-session)
/// surfaces as ReindexRequiredException. The store never creates the collection: a missing
/// or embedding-mismatched store refuses every operation with ReindexRequiredException
/// until `grimoire init` or `grimoire reindex` rebuilds it.
/// </summary>
public sealed class QdrantMemoryStore : IMemoryStore
{
    private const uint ScrollPageSize = 256;

    private const int UpsertBatchSize = 256;

    private const string UpstreamName = "qdrant";

    private readonly IQdrantVectorDb _db;
    private readonly string _collection;
    private readonly EmbeddingFingerprint _fingerprint;
    private readonly QdrantCollectionBootstrapper _bootstrapper;
    private readonly SemaphoreSlim _readyGate = new(1, 1);
    private bool _ready;

    public QdrantMemoryStore(IQdrantVectorDb db, string collection, EmbeddingOptions embedding)
    {
        _db = db;
        _collection = collection;
        _fingerprint = EmbeddingFingerprint.From(embedding.Model, embedding.Dimensions);
        _bootstrapper = new QdrantCollectionBootstrapper(db, collection);
    }

    public QdrantMemoryStore(QdrantOptions qdrant, EmbeddingOptions embedding)
        : this(new QdrantVectorDb(qdrant.Endpoint, qdrant.ApiKey), qdrant.Collection, embedding)
    {
    }

    public async Task UpsertAsync(MemoryRecord record, ReadOnlyMemory<float> vector, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);
        await GuardAsync(() => _db.UpsertAsync(_collection, [Point(record, vector)], cancellationToken));
    }

    public async Task UpdateAsync(MemoryRecord record, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);
        await GuardAsync(() => _db.SetPayloadAsync(_collection, MemoryPayloadMapper.ToPayload(record), record.Id, cancellationToken));
    }

    public async Task<MemoryRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);
        var points = await GuardAsync(() => _db.RetrieveAsync(_collection, [id], withVectors: false, cancellationToken));

        return points.Count == 0 ? null : Decode(points[0]);
    }

    public async Task<IReadOnlyList<MemoryRecord>> ListAsync(MemoryFilter filter, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);
        var points = await ScrollAllAsync(MemoryFilterTranslator.Translate(filter), withVectors: false, cancellationToken);

        IEnumerable<MemoryRecord> records = points
            .Select(Decode)
            .OrderByDescending(record => record.CreatedAt)
            .ThenBy(record => record.Id);

        if (filter.Limit is { } limit)
        {
            records = records.Take(limit);
        }

        return [.. records];
    }

    public async Task<IReadOnlyList<MemoryPoint>> FetchAsync(MemoryFilter filter, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);
        var points = await ScrollAllAsync(MemoryFilterTranslator.Translate(filter), withVectors: true, cancellationToken);

        IEnumerable<MemoryPoint> mapped = points
            .Select(ToPoint)
            .OrderByDescending(point => point.Record.CreatedAt)
            .ThenBy(point => point.Record.Id);

        if (filter.Limit is { } limit)
        {
            mapped = mapped.Take(limit);
        }

        return [.. mapped];
    }

    public async Task<IReadOnlyList<VectorMatch>> SearchAsync(ReadOnlyMemory<float> vector, MemoryFilter filter, int limit, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);

        if (limit <= 0)
        {
            return [];
        }

        var matches = await GuardAsync(() => _db.SearchAsync(_collection, vector, MemoryFilterTranslator.Translate(filter), limit, cancellationToken));

        return [.. matches.Select(match => new VectorMatch
        {
            Record = Decode(match),
            Similarity = match.Score,
        })];
    }

    public async Task<long> CountAsync(MemoryFilter filter, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);
        return await GuardAsync(() => _db.CountAsync(_collection, MemoryFilterTranslator.Translate(filter), cancellationToken));
    }

    public async Task DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);

        if (ids.Count == 0)
        {
            return;
        }

        await GuardAsync(() => _db.DeleteAsync(_collection, ids, cancellationToken));
    }

    public async Task<DeleteByTomeResult> DeleteByTomeAsync(string tome, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);

        var filter = new Filter();
        filter.Must.Add(Conditions.MatchKeyword(MemoryPayloadFields.Tome, tome));

        var ids = new List<Guid>();
        var active = 0;
        var superseded = 0;
        var tombstoned = 0;
        PointId? offset = null;

        do
        {
            var page = await GuardAsync(() => _db.ScrollAsync(_collection, filter, ScrollPageSize, offset, withVectors: false, cancellationToken));

            foreach (var point in page.Result)
            {
                var id = PointId(point.Id);
                ids.Add(id);

                switch (MemoryPayloadMapper.ToRecord(id, point.Payload).Status)
                {
                    case MemoryStatus.Active:
                        active++;
                        break;
                    case MemoryStatus.Superseded:
                        superseded++;
                        break;
                    case MemoryStatus.Tombstoned:
                        tombstoned++;
                        break;
                }
            }

            offset = page.NextPageOffset;
        }
        while (offset is not null);

        if (ids.Count > 0)
        {
            await GuardAsync(() => _db.DeleteAsync(_collection, ids, cancellationToken));
        }

        return new DeleteByTomeResult { Active = active, Superseded = superseded, Tombstoned = tombstoned };
    }

    public async IAsyncEnumerable<MemoryPoint> EnumerateAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);

        PointId? offset = null;

        do
        {
            var page = await GuardAsync(() => _db.ScrollAsync(_collection, new Filter(), ScrollPageSize, offset, withVectors: true, cancellationToken));

            foreach (var point in page.Result)
            {
                if (PointId(point.Id) == EmbeddingFingerprint.MarkerPointId)
                {
                    continue;
                }

                yield return ToPoint(point);
            }

            offset = page.NextPageOffset;
        }
        while (offset is not null);
    }

    public async Task UpsertBatchAsync(IReadOnlyList<MemoryPoint> points, CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);

        for (var start = 0; start < points.Count; start += UpsertBatchSize)
        {
            var batch = points
                .Skip(start)
                .Take(UpsertBatchSize)
                .Select(point => Point(point.Record, point.Vector))
                .ToArray();

            await GuardAsync(() => _db.UpsertAsync(_collection, batch, cancellationToken));
        }
    }

    private async Task<IReadOnlyList<RetrievedPoint>> ScrollAllAsync(Filter filter, bool withVectors, CancellationToken cancellationToken)
    {
        var collected = new List<RetrievedPoint>();
        PointId? offset = null;

        do
        {
            var page = await GuardAsync(() => _db.ScrollAsync(_collection, filter, ScrollPageSize, offset, withVectors, cancellationToken));
            collected.AddRange(page.Result);
            offset = page.NextPageOffset;
        }
        while (offset is not null);

        return collected;
    }

    private async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (_ready)
        {
            return;
        }

        await _readyGate.WaitAsync(cancellationToken);

        try
        {
            if (_ready)
            {
                return;
            }

            await GuardAsync(() => _bootstrapper.EnsurePresentAsync(_fingerprint, cancellationToken));
            _ready = true;
        }
        finally
        {
            _readyGate.Release();
        }
    }

    private async Task GuardAsync(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (RpcException ex) when (IsAvailabilityFailure(ex))
        {
            throw new UpstreamUnavailableException(UpstreamName, ex);
        }
        catch (RpcException ex) when (ex.Status.StatusCode == StatusCode.NotFound)
        {
            throw DroppedMidSession();
        }
    }

    private async Task<T> GuardAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (RpcException ex) when (IsAvailabilityFailure(ex))
        {
            throw new UpstreamUnavailableException(UpstreamName, ex);
        }
        catch (RpcException ex) when (ex.Status.StatusCode == StatusCode.NotFound)
        {
            throw DroppedMidSession();
        }
    }

    private ReindexRequiredException DroppedMidSession() =>
        new($"Collection '{_collection}' disappeared while this process was running — it was dropped externally. " +
            "Run `grimoire init` or `grimoire reindex`; operations stay refused until then.");

    private static bool IsAvailabilityFailure(RpcException ex) =>
        ex.Status.StatusCode is StatusCode.Unavailable
            or StatusCode.DeadlineExceeded
            or StatusCode.Unauthenticated;

    private PointStruct Point(MemoryRecord record, ReadOnlyMemory<float> vector)
    {
        var point = new PointStruct { Id = record.Id, Vectors = vector.ToArray() };

        foreach (var pair in MemoryPayloadMapper.ToPayload(record))
        {
            point.Payload.Add(pair.Key, pair.Value);
        }

        point.Payload[MemoryPayloadFields.VectorModel] = _fingerprint.Model;
        return point;
    }

    private static MemoryRecord Decode(RetrievedPoint point) =>
        MemoryPayloadMapper.ToRecord(PointId(point.Id), point.Payload);

    private static MemoryRecord Decode(ScoredPoint point) =>
        MemoryPayloadMapper.ToRecord(PointId(point.Id), point.Payload);

    private static MemoryPoint ToPoint(RetrievedPoint point)
    {
        if (point.Vectors?.Vector is not { } vector
            || vector.VectorCase != VectorOutput.VectorOneofCase.Dense)
        {
            throw new PayloadDecodeException($"Memory point '{point.Id}' carries no dense vector.");
        }

        return new MemoryPoint
        {
            Record = Decode(point),
            Vector = vector.Dense.Data.ToArray(),
        };
    }

    private static Guid PointId(PointId pointId) =>
        Guid.Parse(pointId.Uuid ?? throw new PayloadDecodeException("Qdrant returned a numeric point id where a UUID is expected."));
}
