namespace Grimoire.Core;

public sealed class MemoryService(
    IMemoryStore store,
    IEmbeddingProvider embeddings,
    IClock clock,
    MemoryEngineOptions? options = null)
{
    private const int DuplicateProbeLimit = 5;

    private readonly MemoryEngineOptions _options = options ?? new();

    public async Task<RememberResult> RememberAsync(RememberRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ArgumentException("Memory content must not be empty.", nameof(request));
        }

        var now = clock.UtcNow;
        var vector = await embeddings.EmbedAsync(request.Content, cancellationToken);
        var duplicates = await ProbeDuplicatesAsync(request, vector, cancellationToken);

        var record = new MemoryRecord
        {
            Id = Guid.NewGuid(),
            Content = request.Content,
            Scope = request.Scope,
            Kind = request.Kind,
            Actor = request.Actor,
            ProjectId = request.ProjectId,
            SessionId = request.SessionId,
            Tags = [.. request.Tags],
            Importance = Math.Clamp(request.Importance, 0, 1),
            CreatedAt = now,
            LastAccessedAt = now,
            ExpiresAt = request.Scope == MemoryScope.Session ? now.AddHours(_options.SessionTtlHours) : null,
            Sources = [.. request.Sources],
        };

        await store.UpsertAsync(record, vector, cancellationToken);

        Guid? supersededId = null;
        foreach (var duplicate in duplicates)
        {
            supersededId ??= duplicate.Id;
            await store.UpdateAsync(duplicate with
            {
                Status = MemoryStatus.Superseded,
                SupersededBy = record.Id,
            }, cancellationToken);
        }

        return new RememberResult { Record = record, SupersededId = supersededId };
    }

    public async Task<IReadOnlyList<RecallMatch>> RecallAsync(RecallQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Text))
        {
            throw new ArgumentException("Recall query text must not be empty.", nameof(query));
        }

        if (query.Limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Recall limit must be positive.");
        }

        var now = clock.UtcNow;
        var vector = await embeddings.EmbedAsync(query.Text, cancellationToken);

        var filter = new MemoryFilter
        {
            Scopes = query.Scopes.Count > 0 ? query.Scopes : MemoryScopes.LongTerm,
            Kind = query.Kind,
            Status = MemoryStatus.Active,
            Tags = query.Tags,
            ProjectId = query.ProjectId,
            SessionId = query.SessionId,
        };

        var fetchLimit = query.Limit * Math.Max(_options.RecallOversampling, 1);
        var matches = await store.SearchAsync(vector, filter, fetchLimit, cancellationToken);

        var top = matches
            .Select(match => (Match: match, Score: MemoryScorer.Score(match.Similarity, match.Record, now, _options.RecencyHalfLives)))
            .OrderByDescending(pair => pair.Score)
            .ThenByDescending(pair => pair.Match.Record.CreatedAt)
            .ThenBy(pair => pair.Match.Record.Id)
            .Take(query.Limit)
            .ToArray();

        var results = new List<RecallMatch>(top.Length);
        foreach (var (match, score) in top)
        {
            var accessed = match.Record with
            {
                AccessCount = match.Record.AccessCount + 1,
                LastAccessedAt = now,
            };

            await store.UpdateAsync(accessed, cancellationToken);
            results.Add(new RecallMatch { Record = accessed, Similarity = match.Similarity, Score = score });
        }

        return results;
    }

    public async Task<bool> ForgetAsync(Guid id, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A tombstone reason is required.", nameof(reason));
        }

        var record = await store.GetAsync(id, cancellationToken);
        if (record is null || record.Status == MemoryStatus.Tombstoned)
        {
            return false;
        }

        await store.UpdateAsync(record with
        {
            Status = MemoryStatus.Tombstoned,
            TombstoneReason = reason,
            TombstonedAt = clock.UtcNow,
        }, cancellationToken);

        return true;
    }

    public Task<IReadOnlyList<MemoryRecord>> ListAsync(MemoryFilter filter, CancellationToken cancellationToken)
    {
        return store.ListAsync(filter, cancellationToken);
    }

    public async Task<GcResult> GcAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var expired = await store.ListAsync(new MemoryFilter
        {
            Scopes = [MemoryScope.Session],
            ExpiresBefore = now,
        }, cancellationToken);

        var aged = await store.ListAsync(new MemoryFilter
        {
            Status = MemoryStatus.Tombstoned,
            TombstonedBefore = now.AddDays(-_options.TombstoneRetentionDays),
        }, cancellationToken);

        var ids = expired.Select(record => record.Id)
            .Concat(aged.Select(record => record.Id))
            .Distinct()
            .ToArray();

        if (ids.Length > 0)
        {
            await store.DeleteAsync(ids, cancellationToken);
        }

        return new GcResult { ExpiredSessions = expired.Count, AgedTombstones = aged.Count };
    }

    public async Task<MemoryStats> StatsAsync(CancellationToken cancellationToken)
    {
        var active = await store.CountAsync(new MemoryFilter { Status = MemoryStatus.Active }, cancellationToken);
        var superseded = await store.CountAsync(new MemoryFilter { Status = MemoryStatus.Superseded }, cancellationToken);
        var tombstoned = await store.CountAsync(new MemoryFilter { Status = MemoryStatus.Tombstoned }, cancellationToken);

        var activeByScope = new Dictionary<MemoryScope, long>();
        foreach (var scope in MemoryScopes.All)
        {
            activeByScope[scope] = await store.CountAsync(new MemoryFilter
            {
                Status = MemoryStatus.Active,
                Scopes = [scope],
            }, cancellationToken);
        }

        var journal = await FetchCompactableJournalAsync(cancellationToken);
        var pending = CompactionPlanner.Plan(journal, clock.UtcNow, _options.Compaction)
            .Sum(group => group.MemoryIds.Count);

        return new MemoryStats
        {
            Active = active,
            Superseded = superseded,
            Tombstoned = tombstoned,
            ActiveByScope = activeByScope,
            PendingCompaction = pending,
        };
    }

    public async Task<IReadOnlyList<CompactionCandidateGroup>> GetCompactionCandidatesAsync(CancellationToken cancellationToken)
    {
        var journal = await FetchCompactableJournalAsync(cancellationToken);
        return CompactionPlanner.Plan(journal, clock.UtcNow, _options.Compaction);
    }

    private async Task<IReadOnlyList<MemoryPoint>> FetchCompactableJournalAsync(CancellationToken cancellationToken)
    {
        return await store.FetchAsync(new MemoryFilter
        {
            Scopes = MemoryScopes.LongTerm,
            Kind = MemoryKind.Journal,
            Status = MemoryStatus.Active,
        }, cancellationToken);
    }

    private async Task<IReadOnlyList<MemoryRecord>> ProbeDuplicatesAsync(RememberRequest request, ReadOnlyMemory<float> vector, CancellationToken cancellationToken)
    {
        var filter = new MemoryFilter
        {
            Scopes = [request.Scope],
            Kind = request.Kind,
            Status = MemoryStatus.Active,
            ProjectId = request.ProjectId,
            SessionId = request.Scope == MemoryScope.Session ? request.SessionId : null,
            ExcludeTags = [PersonaSectionExtensions.PersonaTag],
        };

        var matches = await store.SearchAsync(vector, filter, DuplicateProbeLimit, cancellationToken);

        return matches
            .Where(match => match.Similarity >= _options.DedupSimilarityThreshold)
            .Select(match => match.Record)
            .ToArray();
    }
}
