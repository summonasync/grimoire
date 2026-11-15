using System.Runtime.CompilerServices;
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

    public Task<DeleteByTomeResult> DeleteByTomeAsync(string tome, CancellationToken cancellationToken)
    {
        var removed = _points.Values.Where(point => point.Record.Tome == tome).Select(point => point.Record).ToArray();

        foreach (var record in removed)
        {
            _points.Remove(record.Id);
        }

        return Task.FromResult(new DeleteByTomeResult
        {
            Active = removed.Count(record => record.Status == MemoryStatus.Active),
            Superseded = removed.Count(record => record.Status == MemoryStatus.Superseded),
            Tombstoned = removed.Count(record => record.Status == MemoryStatus.Tombstoned),
        });
    }

    public async IAsyncEnumerable<MemoryPoint> EnumerateAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();

        foreach (var point in _points.Values)
        {
            yield return point;
        }
    }

    public Task UpsertBatchAsync(IReadOnlyList<MemoryPoint> points, CancellationToken cancellationToken)
    {
        foreach (var point in points)
        {
            _points[point.Record.Id] = point;
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

        if (!MemoryStatuses.Effective(filter).Contains(record.Status))
        {
            return false;
        }

        if (!MemoryActorSemantics.MatchesExactly(record.Actor, filter.Actor))
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

        if (filter.Tome is not null && record.Tome != filter.Tome)
        {
            return false;
        }

        if (filter.Tomes.Count > 0 && !filter.Tomes.Contains(record.Tome))
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
