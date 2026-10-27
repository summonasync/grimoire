namespace Grimoire.Core;

public sealed class MemoryService(
    IMemoryStore store,
    IEmbeddingProvider embeddings,
    IClock clock,
    MemoryEngineOptions? options = null,
    GcOptions? gcOptions = null,
    IGcStateStore? gcStateStore = null)
{
    private const int DuplicateProbeLimit = 5;

    private readonly MemoryEngineOptions _options = options ?? new();

    private readonly GcOptions _gcOptions = gcOptions ?? new();

    private readonly IGcStateStore? _gcStateStore = gcStateStore;

    public async Task<RememberResult> RememberAsync(RememberRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ArgumentException("Memory content must not be empty.", nameof(request));
        }

        var now = clock.UtcNow;
        var tome = ResolveTome(request.Tome);
        var key = IdempotencyKey.Compute(request, tome);

        var replay = await FindReplayAsync(key, now, cancellationToken);
        if (replay is not null)
        {
            var converged = await ConvergeSupersedesAsync(replay, cancellationToken);
            return new RememberResult { Record = replay, IdempotentReplay = true, MaintenancePending = !converged };
        }

        await ValidateSourcesAsync(request, tome, cancellationToken);

        var vector = await embeddings.EmbedAsync(request.Content, cancellationToken);
        var duplicates = await ProbeDuplicatesAsync(request, tome, vector, cancellationToken);

        var record = new MemoryRecord
        {
            Id = Guid.NewGuid(),
            Content = request.Content,
            Scope = request.Scope,
            Kind = request.Kind,
            Actor = request.Actor,
            Tome = tome,
            ProjectId = request.ProjectId,
            SessionId = request.SessionId,
            Tags = [.. request.Tags],
            Importance = Math.Clamp(request.Importance, 0, 1),
            CreatedAt = now,
            LastAccessedAt = now,
            ExpiresAt = request.Scope == MemoryScope.Session ? now.AddHours(_options.SessionTtlHours) : null,
            Sources = [.. request.Sources],
            IdempotencyKey = key,
        };

        await store.UpsertAsync(record, vector, cancellationToken);

        Guid? supersededId;
        try
        {
            supersededId = await SupersedeDuplicatesAsync(duplicates, record, cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return new RememberResult { Record = record, MaintenancePending = true };
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
            Tome = query.Tomes.Count == 0 ? ResolveTome(query.Tome) : null,
            Tomes = NormalizeTomes(query.Tomes),
            ProjectId = query.ProjectId,
            SessionId = query.SessionId,
            CreatedAfter = query.CreatedAfter,
            CreatedBefore = query.CreatedBefore,
            Actor = query.Actor,
        };

        var fetchLimit = query.Limit * Math.Max(_options.RecallOversampling, 1);
        var matches = await store.SearchAsync(vector, filter, fetchLimit, cancellationToken);
        var kept = matches.Where(match => filter.Matches(match.Record)).ToArray();

        if (kept.Length < query.Limit && matches.Count == fetchLimit)
        {
            var escalated = await store.SearchAsync(vector, filter, fetchLimit * 2, cancellationToken);
            kept = escalated.Where(match => filter.Matches(match.Record)).ToArray();
        }

        var top = kept
            .Select(match => (Match: match, Score: MemoryScorer.Score(
                match.Similarity,
                match.Record,
                now,
                _options.RecencyHalfLives,
                query.CallerProjectId,
                _options.RecallForeignProjectFactor)))
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

    public async Task<bool> ForgetAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken,
        string? tome = null,
        MemoryActor? actor = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A tombstone reason is required.", nameof(reason));
        }

        var resolved = ResolveTome(tome);
        var record = await store.GetAsync(id, cancellationToken);
        if (record is null || record.Tome != resolved || record.Status == MemoryStatus.Tombstoned)
        {
            return false;
        }

        var now = clock.UtcNow;
        await store.UpdateAsync(CorrectionTrail.Append(record with
        {
            Status = MemoryStatus.Tombstoned,
            TombstoneReason = reason,
            TombstonedAt = now,
        }, now, actor ?? MemoryActor.Human, CorrectionAction.Forget, reason), cancellationToken);

        return true;
    }

    public async Task<ForgetTomeResult> ForgetTomeAsync(
        string tome,
        string reason,
        CancellationToken cancellationToken,
        MemoryActor? actor = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A tombstone reason is required.", nameof(reason));
        }

        var resolved = TomeKey.Normalize(tome);
        var filter = new MemoryFilter
        {
            Tome = resolved,
            Status = MemoryStatus.Active,
            Limit = null,
        };
        var total = await store.CountAsync(filter with { Status = null }, cancellationToken);
        var records = await store.ListAsync(filter, cancellationToken);

        var now = clock.UtcNow;
        var by = actor ?? MemoryActor.Human;
        foreach (var record in records)
        {
            await store.UpdateAsync(CorrectionTrail.Append(record with
            {
                Status = MemoryStatus.Tombstoned,
                TombstoneReason = reason,
                TombstonedAt = now,
            }, now, by, CorrectionAction.Forget, reason), cancellationToken);
        }

        return new ForgetTomeResult { Forgotten = records.Count, Skipped = (int)(total - records.Count) };
    }

    public async Task<IReadOnlyList<MemoryRecord>> ListAsync(MemoryFilter filter, CancellationToken cancellationToken)
    {
        var resolved = filter with { Tome = ResolveTome(filter.Tome), Limit = null };
        var records = await store.ListAsync(resolved, cancellationToken);
        var matched = records.Where(resolved.Matches).ToArray();

        return filter.Limit is { } limit ? matched.Take(limit).ToArray() : matched;
    }

    public async Task<RetomeResult> RetomeAsync(
        MemoryFilter filter,
        string targetTome,
        CancellationToken cancellationToken,
        MemoryActor? actor = null)
    {
        var target = TomeKey.Normalize(targetTome);
        var scoped = filter with
        {
            Tome = ResolveTome(filter.Tome),
            Status = MemoryStatus.Active,
            Limit = null,
        };
        var total = await store.CountAsync(scoped with { Status = null }, cancellationToken);
        var records = await store.ListAsync(scoped, cancellationToken);

        var result = await RewriteTomeAsync(records, target, actor, cancellationToken);

        return result with { Skipped = (int)(total - result.Retomed) };
    }

    public async Task<RetomeResult> RetomeAsync(
        IReadOnlyList<Guid> ids,
        string targetTome,
        CancellationToken cancellationToken,
        MemoryActor? actor = null)
    {
        var target = TomeKey.Normalize(targetTome);
        var records = new List<MemoryRecord>(ids.Count);
        var skipped = 0;
        foreach (var id in ids.Distinct())
        {
            var record = await store.GetAsync(id, cancellationToken);
            if (record is null || record.Status != MemoryStatus.Active)
            {
                skipped++;
                continue;
            }

            records.Add(record);
        }

        var result = await RewriteTomeAsync(records, target, actor, cancellationToken);

        return result with { Skipped = skipped };
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

        var journal = await FetchCompactableJournalAsync(tome: null, cancellationToken);
        var pending = CompactionPlanner.Plan(journal, clock.UtcNow, _options.Compaction)
            .Sum(group => group.MemoryIds.Count);

        var census = await CountByTomeAsync(cancellationToken);

        return new MemoryStats
        {
            Active = active,
            Superseded = superseded,
            Tombstoned = tombstoned,
            ActiveByScope = activeByScope,
            PendingCompaction = pending,
            IdempotencyConflicts = await CountIdempotencyConflictsAsync(cancellationToken),
            ByTome = census.ByTome,
            LastWriteByTome = census.LastWriteByTome,
            Gc = await GcHeartbeatAsync(cancellationToken),
        };
    }

    private async Task<GcState> GcHeartbeatAsync(CancellationToken cancellationToken)
    {
        var state = _gcStateStore is null
            ? GcState.Unknown("gc state store is not configured")
            : await _gcStateStore.ReadAsync(cancellationToken);

        return state with { Stale = state.IsStale(_gcOptions.StalenessThreshold, clock.UtcNow) };
    }

    public async Task<IReadOnlyList<CompactionCandidateGroup>> GetCompactionCandidatesAsync(
        CancellationToken cancellationToken,
        string? tome = null)
    {
        var journal = await FetchCompactableJournalAsync(ResolveTome(tome), cancellationToken);
        return CompactionPlanner.Plan(journal, clock.UtcNow, _options.Compaction);
    }

    public async Task<IReadOnlyDictionary<string, long>> PendingCompactionByTomeAsync(CancellationToken cancellationToken)
    {
        var active = await store.ListAsync(new MemoryFilter { Status = MemoryStatus.Active }, cancellationToken);
        var now = clock.UtcNow;
        var pending = new Dictionary<string, long>();

        foreach (var tome in active
            .Select(record => record.Tome)
            .Distinct()
            .OrderBy(tome => tome, StringComparer.Ordinal))
        {
            var journal = await FetchCompactableJournalAsync(tome, cancellationToken);
            pending[tome] = CompactionPlanner.Plan(journal, now, _options.Compaction)
                .Sum(group => group.MemoryIds.Count);
        }

        return pending;
    }

    private string ResolveTome(string? tome) => TomeKey.Normalize(tome ?? _options.DefaultTome);

    private async Task<RetomeResult> RewriteTomeAsync(
        IReadOnlyList<MemoryRecord> records,
        string target,
        MemoryActor? actor,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var resolvedActor = actor ?? MemoryActor.Human;
        var retomed = 0;

        foreach (var record in records)
        {
            if (record.Status != MemoryStatus.Active || record.Tome == target)
            {
                continue;
            }

            await store.UpdateAsync(CorrectionTrail.Append(record with { Tome = target },
                now, resolvedActor, CorrectionAction.Retome, $"tome: {record.Tome} → {target}"), cancellationToken);
            retomed++;
        }

        return new RetomeResult { Retomed = retomed };
    }

    private static IReadOnlyList<string> NormalizeTomes(IReadOnlyList<string> tomes) => tomes.Count == 0
        ? []
        : [.. tomes.Select(TomeKey.Normalize).Distinct().OrderBy(tome => tome, StringComparer.Ordinal)];

    private async Task<IReadOnlyList<MemoryPoint>> FetchCompactableJournalAsync(string? tome, CancellationToken cancellationToken)
    {
        return await store.FetchAsync(new MemoryFilter
        {
            Scopes = MemoryScopes.LongTerm,
            Kind = MemoryKind.Journal,
            Status = MemoryStatus.Active,
            Tome = tome,
        }, cancellationToken);
    }

    private async Task<TomeCensus> CountByTomeAsync(CancellationToken cancellationToken)
    {
        var active = await store.ListAsync(new MemoryFilter { Status = MemoryStatus.Active }, cancellationToken);
        var tombstoned = await store.ListAsync(new MemoryFilter { Status = MemoryStatus.Tombstoned }, cancellationToken);

        var groups = active.Concat(tombstoned)
            .GroupBy(record => record.Tome)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();

        return new TomeCensus(
            groups.ToDictionary(
                group => group.Key,
                group => new TomeStats
                {
                    Active = group.Count(record => record.Status == MemoryStatus.Active),
                    Tombstoned = group.Count(record => record.Status == MemoryStatus.Tombstoned),
                }),
            groups.ToDictionary(
                group => group.Key,
                group => group.Select(LastWriteAt).Max()));
    }

    private static DateTimeOffset LastWriteAt(MemoryRecord record)
    {
        var newest = record.CreatedAt;

        foreach (var correction in record.Corrections)
        {
            if (correction.At > newest)
            {
                newest = correction.At;
            }
        }

        return record.TombstonedAt is { } forgotten && forgotten > newest ? forgotten : newest;
    }

    private sealed record TomeCensus(
        IReadOnlyDictionary<string, TomeStats> ByTome,
        IReadOnlyDictionary<string, DateTimeOffset> LastWriteByTome);

    private async Task<IReadOnlyList<MemoryRecord>> ProbeDuplicatesAsync(
        RememberRequest request,
        string tome,
        ReadOnlyMemory<float> vector,
        CancellationToken cancellationToken)
    {
        return await ProbeDuplicatesAsync(DuplicateProbeFilter(
            tome,
            request.Scope,
            request.Kind,
            request.ProjectId,
            request.Scope == MemoryScope.Session ? request.SessionId : null), vector, excludeId: null, cancellationToken);
    }

    private async Task<IReadOnlyList<MemoryRecord>> ProbeDuplicatesAsync(MemoryFilter filter, ReadOnlyMemory<float> vector, Guid? excludeId, CancellationToken cancellationToken)
    {
        var matches = await store.SearchAsync(vector, filter, DuplicateProbeLimit, cancellationToken);

        return matches
            .Where(match => match.Similarity >= _options.DedupSimilarityThreshold)
            .Select(match => match.Record)
            .Where(record => excludeId is null || record.Id != excludeId)
            .ToArray();
    }

    private MemoryFilter DuplicateProbeFilter(string tome, MemoryScope scope, MemoryKind kind, string? projectId, string? sessionId) => new()
    {
        Scopes = [scope],
        Kind = kind,
        Status = MemoryStatus.Active,
        Tome = tome,
        ProjectId = projectId,
        SessionId = sessionId,
        ExcludeTags = [PersonaSectionExtensions.PersonaTag],
    };

    private async Task<MemoryRecord?> FindReplayAsync(string key, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var candidates = await store.ListAsync(new MemoryFilter
        {
            Status = MemoryStatus.Active,
            IdempotencyKey = key,
            CreatedAfter = now.AddHours(-_options.IdempotencyWindowHours),
        }, cancellationToken);

        return candidates.MinBy(record => (record.CreatedAt, record.Id));
    }

    private async Task<bool> ConvergeSupersedesAsync(MemoryRecord original, CancellationToken cancellationToken)
    {
        try
        {
            var points = await store.FetchAsync(new MemoryFilter
            {
                Status = MemoryStatus.Active,
                IdempotencyKey = original.IdempotencyKey,
            }, cancellationToken);

            var self = points.FirstOrDefault(point => point.Record.Id == original.Id);
            if (self is null)
            {
                return true;
            }

            var duplicates = await ProbeDuplicatesAsync(DuplicateProbeFilter(
                original.Tome,
                original.Scope,
                original.Kind,
                original.ProjectId,
                original.Scope == MemoryScope.Session ? original.SessionId : null), self.Vector, original.Id, cancellationToken);

            await SupersedeDuplicatesAsync(duplicates, original, cancellationToken);
            return true;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return false;
        }
    }

    private async Task<Guid?> SupersedeDuplicatesAsync(IReadOnlyList<MemoryRecord> duplicates, MemoryRecord successor, CancellationToken cancellationToken)
    {
        Guid? supersededId = null;
        foreach (var duplicate in duplicates)
        {
            supersededId ??= duplicate.Id;
            await store.UpdateAsync(CorrectionTrail.Append(duplicate with
            {
                Status = MemoryStatus.Superseded,
                SupersededBy = successor.Id,
            }, clock.UtcNow, successor.Actor, CorrectionAction.Supersede, $"superseded by {successor.Id}"), cancellationToken);
        }

        return supersededId;
    }

    private async Task ValidateSourcesAsync(RememberRequest request, string tome, CancellationToken cancellationToken)
    {
        if (request.Sources.Count == 0)
        {
            return;
        }

        var missing = new List<Guid>();
        foreach (var source in request.Sources.Distinct())
        {
            var record = await store.GetAsync(source, cancellationToken);
            if (record is null || record.Tome != tome)
            {
                missing.Add(source);
            }
        }

        if (missing.Count > 0)
        {
            throw new ArgumentException(
                $"Source memories do not exist in tome '{tome}': {string.Join(", ", missing)}.", nameof(request.Sources));
        }
    }

    private async Task<long> CountIdempotencyConflictsAsync(CancellationToken cancellationToken)
    {
        var recent = await store.ListAsync(new MemoryFilter
        {
            Status = MemoryStatus.Active,
            CreatedAfter = clock.UtcNow.AddHours(-_options.IdempotencyWindowHours),
        }, cancellationToken);

        return recent
            .Select(record => record.IdempotencyKey)
            .OfType<string>()
            .GroupBy(key => key)
            .Sum(group => group.Count() - 1);
    }
}
