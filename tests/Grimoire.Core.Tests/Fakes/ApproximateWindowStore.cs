namespace Grimoire.Core.Tests.Fakes;

/// <summary>
/// ANN-shaped double: past the window the first round asked for, one row the narrow window
/// returned disappears from the result. FakeMemoryStore orders and cuts the same candidate set
/// on every call, so its wide windows are always exact supersets of its narrow ones and the
/// difference between unioning rounds and replacing them is unobservable there.
/// </summary>
public sealed class ApproximateWindowStore(IMemoryStore inner, Guid vanishingId, int firstWindow) : IMemoryStore
{
    public int SearchCallCount { get; private set; }

    public async Task<IReadOnlyList<VectorMatch>> SearchAsync(
        ReadOnlyMemory<float> vector,
        MemoryFilter filter,
        int limit,
        CancellationToken cancellationToken)
    {
        SearchCallCount++;
        var matches = await inner.SearchAsync(vector, filter, limit, cancellationToken);

        return limit > firstWindow
            ? [.. matches.Where(match => match.Record.Id != vanishingId)]
            : matches;
    }

    public Task UpsertAsync(MemoryRecord record, ReadOnlyMemory<float> vector, CancellationToken cancellationToken)
    {
        return inner.UpsertAsync(record, vector, cancellationToken);
    }

    public Task UpdateAsync(MemoryRecord record, CancellationToken cancellationToken)
    {
        return inner.UpdateAsync(record, cancellationToken);
    }

    public Task<MemoryRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return inner.GetAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<MemoryRecord>> ListAsync(MemoryFilter filter, CancellationToken cancellationToken)
    {
        return inner.ListAsync(filter, cancellationToken);
    }

    public Task<IReadOnlyList<MemoryPoint>> FetchAsync(MemoryFilter filter, CancellationToken cancellationToken)
    {
        return inner.FetchAsync(filter, cancellationToken);
    }

    public Task<long> CountAsync(MemoryFilter filter, CancellationToken cancellationToken)
    {
        return inner.CountAsync(filter, cancellationToken);
    }

    public Task DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        return inner.DeleteAsync(ids, cancellationToken);
    }

    public Task<DeleteByTomeResult> DeleteByTomeAsync(string tome, CancellationToken cancellationToken)
    {
        return inner.DeleteByTomeAsync(tome, cancellationToken);
    }

    public IAsyncEnumerable<MemoryPoint> EnumerateAllAsync(CancellationToken cancellationToken) =>
        inner.EnumerateAllAsync(cancellationToken);

    public Task UpsertBatchAsync(IReadOnlyList<MemoryPoint> points, CancellationToken cancellationToken) =>
        inner.UpsertBatchAsync(points, cancellationToken);
}
