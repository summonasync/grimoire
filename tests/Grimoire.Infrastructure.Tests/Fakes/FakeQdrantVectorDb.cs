using Grimoire.Core;
using Grimoire.Infrastructure.Memory;
using Qdrant.Client.Grpc;

namespace Grimoire.Infrastructure.Tests.Fakes;

public sealed class FakeQdrantVectorDb : IQdrantVectorDb
{
    private readonly Dictionary<Guid, StoredPoint> _points = [];

    public bool CollectionExists { get; set; }

    public int CreateCollectionCount { get; private set; }

    public int? CreatedDimensions { get; private set; }

    public List<(string Field, PayloadSchemaType Type)> CreatedIndexes { get; } = [];

    public List<Filter> ScrolledFilters { get; } = [];

    public List<Filter> SearchedFilters { get; } = [];

    public List<Filter> CountedFilters { get; } = [];

    public List<IReadOnlyDictionary<string, Value>> SetPayloads { get; } = [];

    public List<IReadOnlyList<Guid>> DeletedBatches { get; } = [];

    public int UpsertCount { get; private set; }

    public IReadOnlyCollection<Guid> PointIds => _points.Keys;

    public IReadOnlyDictionary<string, Value> PayloadOf(Guid id) => _points[id].Payload;

    public float[] VectorOf(Guid id) => _points[id].Vector;

    public void Seed(Guid id, float[] vector, IReadOnlyDictionary<string, Value>? payload = null)
    {
        CollectionExists = true;
        _points[id] = new StoredPoint(new Dictionary<string, Value>(payload ?? new Dictionary<string, Value>()), vector);
    }

    public Task<bool> CollectionExistsAsync(string collection, CancellationToken cancellationToken) =>
        Task.FromResult(CollectionExists);

    public Task<CollectionInfo> GetCollectionInfoAsync(string collection, CancellationToken cancellationToken)
    {
        var info = new CollectionInfo { PointsCount = (ulong)_points.Count };

        foreach (var (field, type) in CreatedIndexes)
        {
            info.PayloadSchema[field] = new PayloadSchemaInfo { DataType = type };
        }

        return Task.FromResult(info);
    }

    public Task CreateCollectionAsync(string collection, int dimensions, CancellationToken cancellationToken)
    {
        CreateCollectionCount++;
        CreatedDimensions = dimensions;
        CollectionExists = true;
        return Task.CompletedTask;
    }

    public Task CreatePayloadIndexAsync(string collection, string field, PayloadSchemaType type, CancellationToken cancellationToken)
    {
        CreatedIndexes.Add((field, type));
        return Task.CompletedTask;
    }

    public Task UpsertAsync(string collection, IReadOnlyList<PointStruct> points, CancellationToken cancellationToken)
    {
        UpsertCount++;

        foreach (var point in points)
        {
            var id = Guid.Parse(point.Id.Uuid);
            var payload = new Dictionary<string, Value>();

            foreach (var pair in point.Payload)
            {
                payload[pair.Key] = pair.Value;
            }

            var vector = point.Vectors?.Vector?.Dense?.Data.ToArray() ?? [];
            _points[id] = new StoredPoint(payload, vector);
        }

        return Task.CompletedTask;
    }

    public Task SetPayloadAsync(string collection, IReadOnlyDictionary<string, Value> payload, Guid id, CancellationToken cancellationToken)
    {
        SetPayloads.Add(new Dictionary<string, Value>(payload));

        if (_points.TryGetValue(id, out var stored))
        {
            foreach (var pair in payload)
            {
                stored.Payload[pair.Key] = pair.Value;
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RetrievedPoint>> RetrieveAsync(string collection, IReadOnlyList<Guid> ids, bool withVectors, CancellationToken cancellationToken)
    {
        IReadOnlyList<RetrievedPoint> result = ids
            .Where(_points.ContainsKey)
            .Select(id => Point(id, withVectors))
            .ToArray();

        return Task.FromResult(result);
    }

    public Task<ScrollResponse> ScrollAsync(string collection, Filter filter, uint limit, PointId? offset, bool withVectors, CancellationToken cancellationToken)
    {
        ScrolledFilters.Add(filter);
        var matching = Matching(filter);
        var start = offset is null ? 0 : matching.IndexOf(Guid.Parse(offset.Uuid)) + 1;
        var page = matching.Skip(start).Take((int)limit).ToArray();
        var response = new ScrollResponse();

        foreach (var id in page)
        {
            response.Result.Add(Point(id, withVectors));
        }

        if (start + page.Length < matching.Count)
        {
            response.NextPageOffset = page[^1];
        }

        return Task.FromResult(response);
    }

    public Task<IReadOnlyList<ScoredPoint>> SearchAsync(string collection, ReadOnlyMemory<float> vector, Filter filter, int limit, CancellationToken cancellationToken)
    {
        SearchedFilters.Add(filter);

        IReadOnlyList<ScoredPoint> result = _points
            .Where(pair => QdrantFilterEvaluator.Matches(filter, pair.Value.Payload))
            .Select(pair => (Id: pair.Key, Score: VectorMath.CosineSimilarity(vector.Span, pair.Value.Vector)))
            .OrderByDescending(pair => pair.Score)
            .ThenBy(pair => pair.Id)
            .Take(limit)
            .Select(pair =>
            {
                var scored = new ScoredPoint { Id = pair.Id, Score = (float)pair.Score };

                foreach (var value in _points[pair.Id].Payload)
                {
                    scored.Payload.Add(value.Key, value.Value);
                }

                return scored;
            })
            .ToArray();

        return Task.FromResult(result);
    }

    public Task<long> CountAsync(string collection, Filter filter, CancellationToken cancellationToken)
    {
        CountedFilters.Add(filter);
        return Task.FromResult((long)Matching(filter).Count);
    }

    public Task DeleteAsync(string collection, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        DeletedBatches.Add(ids);

        foreach (var id in ids)
        {
            _points.Remove(id);
        }

        return Task.CompletedTask;
    }

    private List<Guid> Matching(Filter filter) =>
        _points.Where(pair => QdrantFilterEvaluator.Matches(filter, pair.Value.Payload)).Select(pair => pair.Key).ToList();

    private RetrievedPoint Point(Guid id, bool withVectors)
    {
        var stored = _points[id];
        var point = new RetrievedPoint { Id = id };

        foreach (var pair in stored.Payload)
        {
            point.Payload.Add(pair.Key, pair.Value);
        }

        if (withVectors)
        {
            point.Vectors = new VectorsOutput
            {
                Vector = new VectorOutput { Dense = new DenseVector { Data = { stored.Vector } } },
            };
        }

        return point;
    }

    private sealed record StoredPoint(Dictionary<string, Value> Payload, float[] Vector);
}
