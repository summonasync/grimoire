using Grimoire.Infrastructure.Memory;
using Qdrant.Client.Grpc;

namespace Grimoire.Cli.Tests.Fakes;

public sealed class RecordingVectorDb : IQdrantVectorDb
{
    private readonly Dictionary<Guid, IReadOnlyDictionary<string, Value>> _points = [];

    public bool CollectionExists { get; set; }

    public int CreateCollectionCount { get; private set; }

    public int DroppedCount { get; private set; }

    public int? CreatedDimensions { get; private set; }

    public int? CollectionDimensions { get; set; }

    public List<(string Field, PayloadSchemaType Type)> CreatedIndexes { get; } = [];

    public List<IReadOnlyList<PointStruct>> Upserted { get; } = [];

    public Task<bool> CollectionExistsAsync(string collection, CancellationToken cancellationToken) =>
        Task.FromResult(CollectionExists);

    public Task<CollectionInfo> GetCollectionInfoAsync(string collection, CancellationToken cancellationToken)
    {
        var info = new CollectionInfo { PointsCount = (ulong)_points.Count };

        if (CollectionDimensions is { } dimensions)
        {
            info.Config = new CollectionConfig
            {
                Params = new CollectionParams
                {
                    VectorsConfig = new VectorsConfig { Params = new VectorParams { Size = (ulong)dimensions } },
                },
            };
        }

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
        CollectionDimensions = dimensions;
        CollectionExists = true;
        return Task.CompletedTask;
    }

    public Task DropCollectionAsync(string collection, CancellationToken cancellationToken)
    {
        DroppedCount++;
        CollectionExists = false;
        CollectionDimensions = null;
        _points.Clear();
        return Task.CompletedTask;
    }

    public Task CreatePayloadIndexAsync(string collection, string field, PayloadSchemaType type, CancellationToken cancellationToken)
    {
        CreatedIndexes.Add((field, type));
        return Task.CompletedTask;
    }

    public Task UpsertAsync(string collection, IReadOnlyList<PointStruct> points, CancellationToken cancellationToken)
    {
        Upserted.Add(points);
        foreach (var point in points)
        {
            _points[Guid.Parse(point.Id.Uuid)] = new Dictionary<string, Value>(point.Payload);
        }

        return Task.CompletedTask;
    }

    public Task SetPayloadAsync(string collection, IReadOnlyDictionary<string, Value> payload, Guid id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<RetrievedPoint>> RetrieveAsync(string collection, IReadOnlyList<Guid> ids, bool withVectors, CancellationToken cancellationToken)
    {
        IReadOnlyList<RetrievedPoint> result = ids
            .Where(_points.ContainsKey)
            .Select(id =>
            {
                var point = new RetrievedPoint { Id = id };
                foreach (var pair in _points[id])
                {
                    point.Payload.Add(pair.Key, pair.Value);
                }

                return point;
            })
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<ScrollResponse> ScrollAsync(string collection, Filter filter, uint limit, PointId? offset, bool withVectors, CancellationToken cancellationToken)
    {
        var matching = Matching(filter);
        var start = offset is null ? 0 : matching.IndexOf(Guid.Parse(offset.Uuid)) + 1;
        var page = matching.Skip(start).Take((int)limit).ToArray();
        var response = new ScrollResponse();

        foreach (var id in page)
        {
            var point = new RetrievedPoint { Id = id };
            foreach (var pair in _points[id])
            {
                point.Payload.Add(pair.Key, pair.Value);
            }

            response.Result.Add(point);
        }

        if (start + page.Length < matching.Count)
        {
            response.NextPageOffset = page[^1];
        }

        return Task.FromResult(response);
    }

    public Task<IReadOnlyList<ScoredPoint>> SearchAsync(string collection, ReadOnlyMemory<float> vector, Filter filter, int limit, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<long> CountAsync(string collection, Filter filter, CancellationToken cancellationToken) =>
        Task.FromResult((long)Matching(filter).Count);

    private List<Guid> Matching(Filter filter) => _points
        .Where(pair => filter.Must.All(condition => Matches(condition, pair.Value)))
        .Select(pair => pair.Key)
        .ToList();

    private static bool Matches(Condition condition, IReadOnlyDictionary<string, Value> payload) =>
        condition.Field is { } field && field.Match is { } match && match.MatchValueCase == Match.MatchValueOneofCase.Keyword
            ? payload.TryGetValue(field.Key, out var value)
                && value.KindCase == Value.KindOneofCase.StringValue
                && value.StringValue == match.Keyword
            : throw new NotSupportedException($"Filter condition on '{condition.Field?.Key}' is not supported by the fake.");

    public Task DeleteAsync(string collection, IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
