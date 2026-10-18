using Grimoire.Infrastructure.Memory;
using Qdrant.Client.Grpc;

namespace Grimoire.Cli.Tests.Fakes;

public sealed class RecordingVectorDb : IQdrantVectorDb
{
    private readonly Dictionary<Guid, IReadOnlyDictionary<string, Value>> _points = [];

    public bool CollectionExists { get; set; }

    public int CreateCollectionCount { get; private set; }

    public int? CreatedDimensions { get; private set; }

    public List<(string Field, PayloadSchemaType Type)> CreatedIndexes { get; } = [];

    public List<IReadOnlyList<PointStruct>> Upserted { get; } = [];

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

    public Task<ScrollResponse> ScrollAsync(string collection, Filter filter, uint limit, PointId? offset, bool withVectors, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ScoredPoint>> SearchAsync(string collection, ReadOnlyMemory<float> vector, Filter filter, int limit, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<long> CountAsync(string collection, Filter filter, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task DeleteAsync(string collection, IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
