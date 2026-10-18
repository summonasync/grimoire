using System.Runtime.CompilerServices;

namespace Grimoire.Core.Tests.Fakes;

public sealed class FakeMemoryStore : IMemoryStore
{
    private readonly Dictionary<Guid, StoredPoint> _points = [];

    public int UpsertCount { get; private set; }

    public int UpdateCount { get; private set; }

    public int DeleteCount { get; private set; }

    public IReadOnlyList<MemoryRecord> Records => _points.Values.Select(point => point.Record).ToArray();

    public void Seed(MemoryRecord record, params float[] vector) => _points[record.Id] = new StoredPoint(record, vector);

    public Task UpsertAsync(MemoryRecord record, ReadOnlyMemory<float> vector, CancellationToken cancellationToken)
    {
        UpsertCount++;
        _points[record.Id] = new StoredPoint(record, vector.ToArray());
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MemoryRecord record, CancellationToken cancellationToken)
    {
        if (!_points.TryGetValue(record.Id, out var point))
        {
            throw new KeyNotFoundException($"No stored point for '{record.Id}'.");
        }

        UpdateCount++;
        _points[record.Id] = point with { Record = record };
        return Task.CompletedTask;
    }

    public Task<MemoryRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_points.TryGetValue(id, out var point) ? point.Record : null);
    }

    public Task<IReadOnlyList<MemoryRecord>> ListAsync(MemoryFilter filter, CancellationToken cancellationToken)
    {
        IReadOnlyList<MemoryRecord> records = Matching(filter).Select(point => point.Record).ToList();
        return Task.FromResult(records);
    }

    public Task<IReadOnlyList<MemoryPoint>> FetchAsync(MemoryFilter filter, CancellationToken cancellationToken)
    {
        IReadOnlyList<MemoryPoint> points = Matching(filter)
            .Select(point => new MemoryPoint { Record = point.Record, Vector = point.Vector })
            .ToList();
        return Task.FromResult(points);
    }

    public Task<IReadOnlyList<VectorMatch>> SearchAsync(ReadOnlyMemory<float> vector, MemoryFilter filter, int limit, CancellationToken cancellationToken)
    {
        IReadOnlyList<VectorMatch> matches = Matching(filter)
            .Select(point => new VectorMatch
            {
                Record = point.Record,
                Similarity = VectorMath.CosineSimilarity(vector.Span, point.Vector),
            })
            .OrderByDescending(match => match.Similarity)
            .ThenBy(match => match.Record.Id)
            .Take(limit)
            .ToList();
        return Task.FromResult(matches);
    }

    public Task<long> CountAsync(MemoryFilter filter, CancellationToken cancellationToken)
    {
        return Task.FromResult((long)Matching(filter).Count());
    }

    public Task DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        DeleteCount += ids.Count;
        foreach (var id in ids)
        {
            _points.Remove(id);
        }

        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<MemoryPoint> EnumerateAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();

        foreach (var point in _points.Values)
        {
            yield return new MemoryPoint { Record = point.Record, Vector = point.Vector };
        }
    }

    public Task UpsertBatchAsync(IReadOnlyList<MemoryPoint> points, CancellationToken cancellationToken)
    {
        foreach (var point in points)
        {
            _points[point.Record.Id] = new StoredPoint(point.Record, point.Vector.ToArray());
        }

        return Task.CompletedTask;
    }

    private IEnumerable<StoredPoint> Matching(MemoryFilter filter)
    {
        IEnumerable<StoredPoint> query = _points.Values;

        if (filter.Scopes.Count > 0)
        {
            query = query.Where(point => filter.Scopes.Contains(point.Record.Scope));
        }

        if (filter.Kind is not null)
        {
            query = query.Where(point => point.Record.Kind == filter.Kind);
        }

        if (filter.Status is not null)
        {
            query = query.Where(point => point.Record.Status == filter.Status);
        }

        if (filter.Tags.Count > 0)
        {
            query = query.Where(point => filter.Tags.Any(tag => point.Record.Tags.Contains(tag)));
        }

        if (filter.ExcludeTags.Count > 0)
        {
            query = query.Where(point => filter.ExcludeTags.All(tag => !point.Record.Tags.Contains(tag)));
        }

        if (filter.Tome is not null)
        {
            query = query.Where(point => point.Record.Tome == filter.Tome);
        }

        if (filter.Tomes.Count > 0)
        {
            query = query.Where(point => filter.Tomes.Contains(point.Record.Tome));
        }

        if (filter.ProjectId is not null)
        {
            query = query.Where(point => point.Record.ProjectId == filter.ProjectId);
        }

        if (filter.SessionId is not null)
        {
            query = query.Where(point => point.Record.SessionId == filter.SessionId);
        }

        if (filter.ExpiresBefore is not null)
        {
            query = query.Where(point => point.Record.ExpiresAt is not null && point.Record.ExpiresAt <= filter.ExpiresBefore);
        }

        if (filter.TombstonedBefore is not null)
        {
            query = query.Where(point => point.Record.TombstonedAt is not null && point.Record.TombstonedAt <= filter.TombstonedBefore);
        }

        if (filter.IdempotencyKey is not null)
        {
            query = query.Where(point => point.Record.IdempotencyKey == filter.IdempotencyKey);
        }

        if (filter.CreatedAfter is not null)
        {
            query = query.Where(point => point.Record.CreatedAt >= filter.CreatedAfter);
        }

        if (filter.Limit is not null)
        {
            query = query.Take(filter.Limit.Value);
        }

        return query;
    }

    private sealed record StoredPoint(MemoryRecord Record, float[] Vector);
}
