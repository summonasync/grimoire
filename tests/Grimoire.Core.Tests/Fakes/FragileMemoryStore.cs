namespace Grimoire.Core.Tests.Fakes;

public sealed class FragileMemoryStore(IMemoryStore inner) : IMemoryStore
{
    private int _updateFailuresRemaining;

    public List<MemoryRecord> Updated { get; } = [];

    public int UpdateFailuresRemaining
    {
        get => _updateFailuresRemaining;
        set => _updateFailuresRemaining = value;
    }

    public Task UpsertAsync(MemoryRecord record, ReadOnlyMemory<float> vector, CancellationToken cancellationToken)
    {
        return inner.UpsertAsync(record, vector, cancellationToken);
    }

    public async Task UpdateAsync(MemoryRecord record, CancellationToken cancellationToken)
    {
        if (_updateFailuresRemaining > 0)
        {
            _updateFailuresRemaining--;
            throw new InvalidOperationException("Store update failed.");
        }

        await inner.UpdateAsync(record, cancellationToken);
        Updated.Add(record);
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

    public Task<IReadOnlyList<VectorMatch>> SearchAsync(ReadOnlyMemory<float> vector, MemoryFilter filter, int limit, CancellationToken cancellationToken)
    {
        return inner.SearchAsync(vector, filter, limit, cancellationToken);
    }

    public Task<long> CountAsync(MemoryFilter filter, CancellationToken cancellationToken)
    {
        return inner.CountAsync(filter, cancellationToken);
    }

    public Task DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        return inner.DeleteAsync(ids, cancellationToken);
    }
}
