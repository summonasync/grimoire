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

    /// <summary>
    /// Ops enumeration of the whole store: every status, every tome, vectors included,
    /// service marker points excluded, no ordering guarantee; pages internally so neither
    /// side buffers the store. Used by export-class sweeps, not by filtered reads.
    /// </summary>
    IAsyncEnumerable<MemoryPoint> EnumerateAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Batch upsert of record+vector pairs that keeps ids, statuses and timestamps as
    /// recorded and stores vectors as-is. Import path: callers must have validated the
    /// vectors against the configured embedding model.
    /// </summary>
    Task UpsertBatchAsync(IReadOnlyList<MemoryPoint> points, CancellationToken cancellationToken);
}
