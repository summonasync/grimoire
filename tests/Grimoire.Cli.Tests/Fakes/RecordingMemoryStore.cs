using Grimoire.Core;

namespace Grimoire.Cli.Tests.Fakes;

public sealed class RecordingMemoryStore : IMemoryStore
{
    private readonly Dictionary<Guid, StoredPoint> _points = [];

    public List<MemoryFilter> ListedFilters { get; } = [];

    public List<(ReadOnlyMemory<float> Vector, MemoryFilter Filter, int Limit)> Searched { get; } = [];

    public List<MemoryRecord> Upserted { get; } = [];

    public List<MemoryRecord> Updated { get; } = [];

    public List<IReadOnlyList<Guid>> Deleted { get; } = [];

    public Exception? UpdateFailure { get; set; }

    public IReadOnlyList<MemoryRecord> Records => _points.Values.Select(point => point.Record).ToArray();

    public MemoryRecord Add(
        string content,
        float[] vector,
        MemoryScope scope = MemoryScope.Project,
        MemoryKind kind = MemoryKind.Journal,
        MemoryActor? actor = null,
        MemoryStatus status = MemoryStatus.Active,
        string? projectId = null,
        string? sessionId = null,
        IReadOnlyList<string>? tags = null,
        DateTimeOffset? expiresAt = null,
        Guid? supersededBy = null,
        string? tombstoneReason = null,
        DateTimeOffset? tombstonedAt = null)
    {
        var record = new MemoryRecord
        {
            Id = Guid.NewGuid(),
            Content = content,
            Scope = scope,
            Kind = kind,
            Actor = actor ?? MemoryActor.Human,
            ProjectId = projectId,
            SessionId = sessionId,
            Tags = tags ?? [],
            CreatedAt = FakeClock.DefaultStart,
            LastAccessedAt = FakeClock.DefaultStart,
            ExpiresAt = expiresAt,
            Status = status,
            SupersededBy = supersededBy,
            TombstoneReason = tombstoneReason,
            TombstonedAt = tombstonedAt,
        };

        Seed(record, vector);
        return record;
    }

    public void Seed(MemoryRecord record, params float[] vector) => _points[record.Id] = new StoredPoint(record, vector);

    public Task UpsertAsync(MemoryRecord record, ReadOnlyMemory<float> vector, CancellationToken cancellationToken)
    {
        Upserted.Add(record);
        _points[record.Id] = new StoredPoint(record, vector.ToArray());
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MemoryRecord record, CancellationToken cancellationToken)
    {
        if (UpdateFailure is not null)
        {
            throw UpdateFailure;
        }

        if (!_points.TryGetValue(record.Id, out var point))
        {
            throw new KeyNotFoundException($"No stored point for '{record.Id}'.");
        }

        Updated.Add(record);
        _points[record.Id] = point with { Record = record };
        return Task.CompletedTask;
    }

    public Task<MemoryRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_points.TryGetValue(id, out var point) ? point.Record : null);
    }

    public Task<IReadOnlyList<MemoryRecord>> ListAsync(MemoryFilter filter, CancellationToken cancellationToken)
    {
        ListedFilters.Add(filter);
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
        Searched.Add((vector, filter, limit));
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
        Deleted.Add(ids);
        foreach (var id in ids)
        {
            _points.Remove(id);
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
