using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

internal sealed class FakeMemoryStore : IMemoryStore
{
    private readonly Dictionary<Guid, MemoryPoint> _points = [];

    public IReadOnlyCollection<MemoryPoint> Points => _points.Values;

    public List<IReadOnlyList<Guid>> DeletedBatches { get; } = [];

    public Exception? UpdateFailure { get; set; }

    public async Task UpsertAsync(MemoryRecord record, ReadOnlyMemory<float> vector, CancellationToken cancellationToken)
    {
        await Task.Yield();
        _points[record.Id] = new MemoryPoint { Record = record, Vector = vector };
    }

    public Task UpdateAsync(MemoryRecord record, CancellationToken cancellationToken)
    {
        if (UpdateFailure is not null)
        {
            throw UpdateFailure;
        }

        if (!_points.TryGetValue(record.Id, out var point))
        {
            throw new InvalidOperationException($"Update of unknown point {record.Id}.");
        }

        _points[record.Id] = point with { Record = record };
        return Task.CompletedTask;
    }

    public Task<MemoryRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_points.TryGetValue(id, out var point) ? point.Record : null);
    }

    public Task<IReadOnlyList<MemoryRecord>> ListAsync(MemoryFilter filter, CancellationToken cancellationToken)
    {
        IReadOnlyList<MemoryRecord> records = Matches(filter).Select(point => point.Record).ToList();
        return Task.FromResult(records);
    }

    public Task<IReadOnlyList<MemoryPoint>> FetchAsync(MemoryFilter filter, CancellationToken cancellationToken)
    {
        IReadOnlyList<MemoryPoint> points = Matches(filter).ToList();
        return Task.FromResult(points);
    }

    public Task<IReadOnlyList<VectorMatch>> SearchAsync(ReadOnlyMemory<float> vector, MemoryFilter filter, int limit, CancellationToken cancellationToken)
    {
        IReadOnlyList<VectorMatch> matches = Matches(filter)
            .Select(point => new VectorMatch
            {
                Record = point.Record,
                Similarity = VectorMath.CosineSimilarity(vector.Span, point.Vector.Span),
            })
            .OrderByDescending(match => match.Similarity)
            .ThenBy(match => match.Record.Id)
            .Take(limit)
            .ToList();

        return Task.FromResult(matches);
    }

    public Task<long> CountAsync(MemoryFilter filter, CancellationToken cancellationToken)
    {
        return Task.FromResult((long)Matches(filter).Count());
    }

    public Task DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        DeletedBatches.Add(ids);
        foreach (var id in ids)
        {
            _points.Remove(id);
        }

        return Task.CompletedTask;
    }

    private IEnumerable<MemoryPoint> Matches(MemoryFilter filter)
    {
        return _points.Values
            .Where(point => Matches(point.Record, filter))
            .OrderByDescending(point => point.Record.CreatedAt)
            .ThenBy(point => point.Record.Id)
            .Take(filter.Limit ?? int.MaxValue);
    }

    private static bool Matches(MemoryRecord record, MemoryFilter filter)
    {
        if (filter.Scopes.Count > 0 && !filter.Scopes.Contains(record.Scope))
        {
            return false;
        }

        if (filter.Kind is not null && record.Kind != filter.Kind)
        {
            return false;
        }

        if (filter.Status is not null && record.Status != filter.Status)
        {
            return false;
        }

        if (filter.Tags.Count > 0 && !record.Tags.Any(filter.Tags.Contains))
        {
            return false;
        }

        if (filter.ExcludeTags.Count > 0 && record.Tags.Any(filter.ExcludeTags.Contains))
        {
            return false;
        }

        if (filter.ProjectId is not null && record.ProjectId != filter.ProjectId)
        {
            return false;
        }

        if (filter.SessionId is not null && record.SessionId != filter.SessionId)
        {
            return false;
        }

        if (filter.ExpiresBefore is not null && (record.ExpiresAt is null || record.ExpiresAt > filter.ExpiresBefore))
        {
            return false;
        }

        if (filter.TombstonedBefore is not null && (record.TombstonedAt is null || record.TombstonedAt > filter.TombstonedBefore))
        {
            return false;
        }

        if (filter.IdempotencyKey is not null && record.IdempotencyKey != filter.IdempotencyKey)
        {
            return false;
        }

        if (filter.CreatedAfter is not null && record.CreatedAt < filter.CreatedAfter)
        {
            return false;
        }

        return true;
    }
}
