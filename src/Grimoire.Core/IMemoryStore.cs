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
    /// Hard delete of every point whose payload tome equals the given key — any scope, any
    /// status (active, tombstoned, superseded, session-scope included). The scroll filter is
    /// strictly positive (must match tome == key) so points carrying no tome payload — the
    /// embedding fingerprint marker, legacy pre-tome records — can never be deleted here;
    /// must_not or unfiltered deletes are forbidden on this path. Returns per-status counts
    /// of the removed points.
    /// </summary>
    Task<DeleteByTomeResult> DeleteByTomeAsync(string tome, CancellationToken cancellationToken);

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
