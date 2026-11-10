using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Grimoire.Infrastructure.Memory;

public sealed class QdrantVectorDb : IQdrantVectorDb
{
    private readonly QdrantClient _client;

    public QdrantVectorDb(string endpoint, string? apiKey)
        : this(new QdrantClient(new Uri(endpoint), apiKey))
    {
    }

    internal QdrantVectorDb(QdrantClient client) => _client = client;

    public Task<bool> CollectionExistsAsync(string collection, CancellationToken cancellationToken) =>
        _client.CollectionExistsAsync(collection, cancellationToken);

    public Task<CollectionInfo> GetCollectionInfoAsync(string collection, CancellationToken cancellationToken) =>
        _client.GetCollectionInfoAsync(collection, cancellationToken);

    public Task CreateCollectionAsync(string collection, int dimensions, CancellationToken cancellationToken) =>
        // Qdrant rejects zero shard/replication/consistency factors outright; 1/1/1 matches every server default.
        _client.CreateCollectionAsync(
            collection,
            new VectorParams { Size = (ulong)dimensions, Distance = Distance.Cosine },
            1,
            1,
            1,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            cancellationToken);

    public Task CreateCollectionWithoutVectorsAsync(string collection, CancellationToken cancellationToken) =>
        _client.CreateCollectionAsync(
            collection,
            new VectorParamsMap(),
            1,
            1,
            1,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            cancellationToken);

    public Task DropCollectionAsync(string collection, CancellationToken cancellationToken) =>
        _client.DeleteCollectionAsync(collection, null, cancellationToken);

    public Task CreatePayloadIndexAsync(string collection, string field, PayloadSchemaType type, CancellationToken cancellationToken) =>
        _client.CreatePayloadIndexAsync(collection, field, type, null, true, null, cancellationToken);

    public Task UpsertAsync(string collection, IReadOnlyList<PointStruct> points, CancellationToken cancellationToken) =>
        _client.UpsertAsync(collection, points, true, null, null, cancellationToken);

    public Task SetPayloadAsync(string collection, IReadOnlyDictionary<string, Value> payload, Guid id, CancellationToken cancellationToken) =>
        _client.SetPayloadAsync(collection, payload, id, true, null, null, null, cancellationToken);

    public Task<IReadOnlyList<RetrievedPoint>> RetrieveAsync(string collection, IReadOnlyList<Guid> ids, bool withVectors, CancellationToken cancellationToken) =>
        _client.RetrieveAsync(collection, [.. ids.Select(id => (PointId)id)], true, withVectors, null, null, cancellationToken);

    public Task<ScrollResponse> ScrollAsync(string collection, Filter filter, uint limit, PointId? offset, bool withVectors, CancellationToken cancellationToken) =>
        _client.ScrollAsync(
            collection,
            filter,
            limit,
            offset,
            new WithPayloadSelector { Enable = true },
            new WithVectorsSelector { Enable = withVectors },
            null,
            null,
            null,
            cancellationToken);

    public Task<IReadOnlyList<ScoredPoint>> SearchAsync(string collection, ReadOnlyMemory<float> vector, Filter filter, int limit, CancellationToken cancellationToken) =>
        _client.QueryAsync(
            collection,
            (Query)vector.ToArray(),
            [],
            null,
            filter,
            null,
            null,
            (ulong)limit,
            0,
            new WithPayloadSelector { Enable = true },
            new WithVectorsSelector { Enable = false },
            null,
            null,
            null,
            null,
            cancellationToken);

    public async Task<long> CountAsync(string collection, Filter filter, CancellationToken cancellationToken) =>
        (long)await _client.CountAsync(collection, filter, true, null, null, cancellationToken);

    public Task DeleteAsync(string collection, IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        _client.DeleteAsync(collection, ids, true, null, null, cancellationToken);
}
