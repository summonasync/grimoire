using Qdrant.Client.Grpc;

namespace Grimoire.Infrastructure.Memory;

public interface IQdrantVectorDb
{
    Task<bool> CollectionExistsAsync(string collection, CancellationToken cancellationToken);

    Task<CollectionInfo> GetCollectionInfoAsync(string collection, CancellationToken cancellationToken);

    Task CreateCollectionAsync(string collection, int dimensions, CancellationToken cancellationToken);

    Task CreateCollectionWithoutVectorsAsync(string collection, CancellationToken cancellationToken);

    Task DropCollectionAsync(string collection, CancellationToken cancellationToken);

    Task CreatePayloadIndexAsync(string collection, string field, PayloadSchemaType type, CancellationToken cancellationToken);

    Task UpsertAsync(string collection, IReadOnlyList<PointStruct> points, CancellationToken cancellationToken);

    Task SetPayloadAsync(string collection, IReadOnlyDictionary<string, Value> payload, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<RetrievedPoint>> RetrieveAsync(string collection, IReadOnlyList<Guid> ids, bool withVectors, CancellationToken cancellationToken);

    Task<ScrollResponse> ScrollAsync(string collection, Filter filter, uint limit, PointId? offset, bool withVectors, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScoredPoint>> SearchAsync(string collection, ReadOnlyMemory<float> vector, Filter filter, int limit, CancellationToken cancellationToken);

    Task<long> CountAsync(string collection, Filter filter, CancellationToken cancellationToken);

    Task DeleteAsync(string collection, IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
}
