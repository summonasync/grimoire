namespace Grimoire.Core;

public sealed class MemoryService(
    IMemoryStore store,
    IEmbeddingProvider embeddings,
    IClock clock,
    MemoryEngineOptions? options = null,
    GcOptions? gcOptions = null,
    IGcStateStore? gcStateStore = null,
    ITopicClassifier? topicClassifier = null,
    ConsolidationOptions? consolidationOptions = null,
    IConsolidationStateStore? consolidationStateStore = null)
{
    private const int DuplicateProbeLimit = 5;

    private const int NearMatchLimit = 3;

    private const int NearSnippetChars = 160;

    private const double NoteImportance = 0.5;

    private const double ConsolidatedBaseImportance = 0.6;

    private const double ConsolidatedPerSourceImportance = 0.04;

    private readonly MemoryEngineOptions _options = options ?? new();

    private readonly GcOptions _gcOptions = gcOptions ?? new();

    private readonly IGcStateStore? _gcStateStore = gcStateStore;

    private readonly ITopicClassifier? _topicClassifier = topicClassifier;

    private readonly ConsolidationOptions _consolidationOptions = consolidationOptions ?? new();

    private readonly IConsolidationStateStore? _consolidationStateStore = consolidationStateStore;

    private readonly Consolidation.IClusterSummarizer _summarizer = new Consolidation.ExtractiveSummarizer();

    private readonly RecallEscalationLoop _recallWindows = new(store, options);

    public async Task<RememberResult> RememberAsync(RememberRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ArgumentException("Memory content must not be empty.", nameof(request));
        }

        var tome = ResolveTome(request.Tome);
        var replay = await FindReplayAsync(IdempotencyKey.Compute(request, tome), clock.UtcNow, cancellationToken);
        if (replay is not null)
        {
            return await ReplayAsync(replay, cancellationToken);
        }

        return await CommitAsync(request, null, cancellationToken);
    }

    public async Task<NoteResult> NoteAsync(NoteRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ArgumentException("Note content must not be empty.", nameof(request));
        }

        var tome = ResolveTome(request.Tome);
        var replay = await FindReplayAsync(
            IdempotencyKey.Compute(NoteContract(request, null), tome), clock.UtcNow, cancellationToken);
        if (replay is not null)
        {
            return new NoteResult(await ReplayAsync(replay, cancellationToken), null);
        }

        var vector = await embeddings.EmbedAsync(request.Content, cancellationToken);
        var explicitTag = ITopicClassifier.Normalize(request.Topic);
        var vote = explicitTag is not null
            ? new TopicVote { Tag = explicitTag, CandidateTag = explicitTag, Decision = TopicVoteDecision.Explicit }
            : await ClassifyTopicAsync(request, tome, vector, cancellationToken);

        var result = await CommitAsync(NoteContract(request, vote?.Tag), vector, cancellationToken);
        return new NoteResult(result, vote);
    }

    private async Task<RememberResult> ReplayAsync(MemoryRecord replay, CancellationToken cancellationToken)
    {
        var converged = await ConvergeSupersedesAsync(replay, cancellationToken);
        return new RememberResult { Record = replay, IdempotentReplay = true, MaintenancePending = !converged };
    }

    private async Task<RememberResult> CommitAsync(RememberRequest request, ReadOnlyMemory<float>? vector, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var tome = ResolveTome(request.Tome);
        var key = IdempotencyKey.Compute(request, tome);

        await ValidateSourcesAsync(request, tome, cancellationToken);
        var superseded = await ValidateSupersedesAsync(request, tome, cancellationToken);

        var embedded = vector ?? await embeddings.EmbedAsync(request.Content, cancellationToken);
        var matches = await ProbeDuplicatesAsync(request, tome, embedded, cancellationToken);
        var nearMatches = SelectNearMatches(matches, superseded?.Id);

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

        await store.UpsertAsync(record, embedded, cancellationToken);

        Guid? supersededId;
        try
        {
            supersededId = await SupersedeDuplicatesAsync(SupersedeTargets(matches, superseded), record, cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return new RememberResult { Record = record, NearMatches = nearMatches, MaintenancePending = true };
        }

        return new RememberResult { Record = record, SupersededId = supersededId, NearMatches = nearMatches };
    }

    public async Task<RecallResult> RecallAsync(RecallQuery query, CancellationToken cancellationToken)
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

        var scan = await _recallWindows.SearchAsync(vector, filter, query.Limit, cancellationToken);

        var top = scan.Kept
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

        return new RecallResult
        {
            Matches = results,
            Warnings = RoundCapWarning.Build(scan, query.Limit),
        };
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
        var total = await store.CountAsync(filter with { Status = null, Statuses = MemoryStatuses.All }, cancellationToken);
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
        var total = await store.CountAsync(scoped with { Status = null, Statuses = MemoryStatuses.All }, cancellationToken);
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
            Status = MemoryStatus.Active,
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

        // Counted after the delete on purpose: a session tombstone can be past TTL and past
        // retention at once, and then it belongs in AgedTombstones only — reporting the same row
        // as both removed and retained is the dishonesty this figure exists to remove.
        var retained = await store.CountAsync(new MemoryFilter
        {
            Scopes = [MemoryScope.Session],
            ExpiresBefore = now,
            Statuses = [MemoryStatus.Superseded, MemoryStatus.Tombstoned],
        }, cancellationToken);

        return new GcResult
        {
            ExpiredSessions = expired.Count,
            AgedTombstones = aged.Count,
            RetainedPastTtl = (int)retained,
        };
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
            Consolidation = await ConsolidationHeartbeatAsync(cancellationToken),
        };
    }

    private async Task<GcState> GcHeartbeatAsync(CancellationToken cancellationToken)
    {
        var state = _gcStateStore is null
            ? GcState.Unknown("gc state store is not configured")
            : await _gcStateStore.ReadAsync(cancellationToken);

        return state with { Stale = state.IsStale(_gcOptions.StalenessThreshold, clock.UtcNow) };
    }

    private async Task<ConsolidationState> ConsolidationHeartbeatAsync(CancellationToken cancellationToken)
    {
        var state = _consolidationStateStore is null
            ? ConsolidationState.Unknown("consolidation state store is not configured")
            : await _consolidationStateStore.ReadAsync(cancellationToken);

        return state with { Stale = state.IsStale(_consolidationOptions.StalenessThreshold, clock.UtcNow) };
    }

    public async Task<ConsolidationResult> ConsolidateAsync(MemoryActor actor, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var journal = await store.ListAsync(new MemoryFilter
        {
            Status = MemoryStatus.Active,
            Scopes = MemoryScopes.LongTerm,
            Kind = MemoryKind.Journal,
        }, cancellationToken);
        var tomes = journal
            .Select(record => record.Tome)
            .Distinct()
            .OrderBy(tome => tome, StringComparer.Ordinal)
            .ToArray();

        var consolidated = 0;
        foreach (var tome in tomes)
        {
            var plan = CompactionPlanner.Plan(
                await FetchConsolidationJournalAsync(tome, cancellationToken),
                now,
                _options.Compaction with { MinClusterSize = _consolidationOptions.MinConsolidationSize });

            foreach (var group in plan)
            {
                await ConsolidateClusterAsync(group.MemoryIds, tome, actor, now, cancellationToken);
                consolidated++;
            }
        }

        return new ConsolidationResult { Clusters = consolidated };
    }

    private async Task<IReadOnlyList<MemoryPoint>> FetchConsolidationJournalAsync(string tome, CancellationToken cancellationToken)
    {
        var journal = await FetchCompactableJournalAsync(tome, cancellationToken);

        return journal
            .Where(point => !point.Record.Tags.Contains(ConsolidationOptions.Tag))
            .ToArray();
    }

    private async Task ConsolidateClusterAsync(
        IReadOnlyList<Guid> ids,
        string tome,
        MemoryActor actor,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var members = new List<MemoryRecord>(ids.Count);
        foreach (var id in ids)
        {
            if (await store.GetAsync(id, cancellationToken) is { } record
                && record.Status == MemoryStatus.Active
                && record.Tome == tome)
            {
                members.Add(record);
            }
        }

        if (members.Count < _consolidationOptions.MinConsolidationSize)
        {
            return;
        }

        var memberIds = members.Select(member => member.Id).ToHashSet();
        var sources = members
            .Where(member => member.SupersededBy is not { } successor || !memberIds.Contains(successor))
            .OrderBy(member => member.CreatedAt)
            .ThenBy(member => member.Id)
            .ToList();
        var topic = Consolidation.ExtractiveSummarizer.MajorityTopic(sources);
        var fact = await RememberAsync(new RememberRequest
        {
            Content = _summarizer.Summarize(sources),
            Scope = MajorityScope(members),
            Kind = MemoryKind.Fact,
            Actor = actor,
            Tome = tome,
            ProjectId = MajorityProject(members),
            Tags = topic is null ? [ConsolidationOptions.Tag] : [ConsolidationOptions.Tag, topic],
            Importance = Math.Clamp(
                ConsolidatedBaseImportance + ConsolidatedPerSourceImportance * sources.Count,
                ConsolidatedBaseImportance,
                0.9),
            Sources = [.. sources.Select(member => member.Id)],
        }, cancellationToken);

        foreach (var source in sources)
        {
            await store.UpdateAsync(CorrectionTrail.Append(source with
            {
                Tags = [.. source.Tags, ConsolidationOptions.Tag],
            }, now, actor, CorrectionAction.Patch, $"consolidated into {fact.Record.Id}"), cancellationToken);
        }
    }

    private static MemoryScope MajorityScope(IReadOnlyList<MemoryRecord> records) => records
        .GroupBy(record => record.Scope)
        .OrderByDescending(group => group.Count())
        .ThenBy(group => group.Key)
        .First().Key;

    private static string? MajorityProject(IReadOnlyList<MemoryRecord> records) => records
        .GroupBy(record => record.ProjectId ?? string.Empty)
        .OrderByDescending(group => group.Count())
        .ThenBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => group.Key)
        .First() is { Length: > 0 } projectId ? projectId : null;

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

    private async Task<IReadOnlyList<VectorMatch>> ProbeDuplicatesAsync(
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

    private async Task<IReadOnlyList<VectorMatch>> ProbeDuplicatesAsync(MemoryFilter filter, ReadOnlyMemory<float> vector, Guid? excludeId, CancellationToken cancellationToken)
    {
        var matches = await store.SearchAsync(vector, filter, DuplicateProbeLimit, cancellationToken);

        return matches
            .Where(match => match.Similarity >= _options.NearDuplicateSimilarityThreshold)
            .Where(match => excludeId is null || match.Record.Id != excludeId)
            .ToArray();
    }

    private IReadOnlyList<MemoryRecord> DuplicateRecords(IReadOnlyList<VectorMatch> matches) => [.. matches
        .Where(match => match.Similarity >= _options.DedupSimilarityThreshold)
        .Select(match => match.Record)];

    private IReadOnlyList<MemoryRecord> SupersedeTargets(IReadOnlyList<VectorMatch> matches, MemoryRecord? superseded)
    {
        var duplicates = DuplicateRecords(matches);

        return superseded is null
            ? duplicates
            : [superseded, .. duplicates.Where(record => record.Id != superseded.Id)];
    }

    private IReadOnlyList<NearMatch> SelectNearMatches(IReadOnlyList<VectorMatch> matches, Guid? supersededId) => [.. matches
        .Where(match => match.Similarity < _options.DedupSimilarityThreshold)
        .Where(match => match.Record.Id != supersededId)
        .OrderByDescending(match => match.Similarity)
        .ThenBy(match => match.Record.Id)
        .Take(NearMatchLimit)
        .Select(match => new NearMatch
        {
            MemoryId = match.Record.Id,
            Similarity = match.Similarity,
            Snippet = Snippet(match.Record.Content),
        })];

    private static string Snippet(string content)
    {
        var line = content.Split('\n')[0].TrimEnd('\r');

        return line.Length <= NearSnippetChars ? line : line[..NearSnippetChars];
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

            var matches = await ProbeDuplicatesAsync(DuplicateProbeFilter(
                original.Tome,
                original.Scope,
                original.Kind,
                original.ProjectId,
                original.Scope == MemoryScope.Session ? original.SessionId : null), self.Vector, original.Id, cancellationToken);

            await SupersedeDuplicatesAsync(DuplicateRecords(matches), original, cancellationToken);
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

    private async Task<MemoryRecord?> ValidateSupersedesAsync(RememberRequest request, string tome, CancellationToken cancellationToken)
    {
        if (request.Supersedes is not { } id)
        {
            return null;
        }

        var target = await store.GetAsync(id, cancellationToken);

        if (target is null || target.Tome != tome)
        {
            throw new ArgumentException($"Superseded memory does not exist in tome '{tome}': {id}.", nameof(request.Supersedes));
        }

        if (target.Status != MemoryStatus.Active)
        {
            throw new ArgumentException($"Superseded memory is not active: {id}.", nameof(request.Supersedes));
        }

        return target;
    }

    private static RememberRequest NoteContract(NoteRequest request, string? topicTag) => new()
    {
        Content = request.Content,
        Scope = request.Scope,
        Kind = MemoryKind.Journal,
        Actor = request.Actor,
        Tome = request.Tome,
        ProjectId = request.ProjectId,
        SessionId = request.SessionId,
        Tags = topicTag is null ? request.Tags : [.. request.Tags, topicTag],
        Importance = NoteImportance,
    };

    private async Task<TopicVote?> ClassifyTopicAsync(
        NoteRequest request,
        string tome,
        ReadOnlyMemory<float> vector,
        CancellationToken cancellationToken)
    {
        if (_topicClassifier is null)
        {
            return null;
        }

        return await _topicClassifier.ClassifyAsync(
            vector,
            tome,
            request.Scope,
            request.ProjectId,
            request.Scope == MemoryScope.Session ? request.SessionId : null,
            cancellationToken);
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
