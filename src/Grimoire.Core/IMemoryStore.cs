namespace Grimoire.Core;

public interface IMemoryStore
{
    Task UpsertAsync(MemoryRecord record, ReadOnlyMemory<float> vector, CancellationToken cancellationToken);

    Task UpdateAsync(MemoryRecord record, CancellationToken cancellationToken);

    Task<MemoryRecord?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemoryRecord>> ListAsync(MemoryFilter filter, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemoryPoint>> FetchAsync(MemoryFilter filter, CancellationToken cancellationToken);

    Task<IReadOnlyList<VectorMatch>> SearchAsync(ReadOnlyMemory<float> vector, MemoryFilter filter, int limit, CancellationToken cancellationToken);

    Task<long> CountAsync(MemoryFilter filter, CancellationToken cancellationToken);

    Task DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
}
