namespace Grimoire.Core;

public sealed class BriefService(
    IMemoryStore store,
    PersonaService persona,
    ITomeManifestStore manifests,
    IClock clock,
    MemoryEngineOptions? options = null)
{
    private const double NoSimilarity = 1.0;

    private const int LimitHintCeiling = 50;

    private const int DefaultFactsPerScope = 5;

    private const int DefaultJournalPerScope = 5;

    private const int DefaultTombstoneLimit = 10;

    private const int TomeCap = 16;

    private readonly MemoryEngineOptions _options = options ?? new();

    public async Task<BriefResult> BriefAsync(BriefQuery query, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var tome = ResolveTome(query.Tome);
        var scopes = NormalizeScopes(query.Scopes);
        var omitted = new List<BriefSection>();
        var remaining = _options.BriefMaxTotalChars;

        var compactionDue = MeasureCompactionDueAsync(tome, now, cancellationToken);

        BriefSectionState Fit(BriefSection section, int cost)
        {
            if (cost <= 0)
            {
                return BriefSectionState.Empty;
            }

            if (cost > remaining)
            {
                omitted.Add(section);
                return BriefSectionState.Omitted;
            }

            remaining -= cost;
            return BriefSectionState.Populated;
        }

        var document = await persona.GetAsync(cancellationToken, tome);
        var personaState = Fit(BriefSection.Persona, PersonaChars(document));

        var facts = await CollectFactsAsync(query, scopes, tome, now, cancellationToken);
        var factsState = Fit(BriefSection.Facts, facts.Sum(fact => fact.Content.Length));

        var tombstones = await CollectTombstonesAsync(query, scopes, tome, cancellationToken);
        var tombstonesState = Fit(BriefSection.Tombstones, tombstones.Sum(tombstone => tombstone.Reason?.Length ?? 0));

        IReadOnlyList<BriefDailyGroup> dailyGroups = [];
        IReadOnlySet<Guid>? dailyIds = null;
        var dailyMore = 0;
        var dailyFound = false;

        if (query.Daily)
        {
            var daily = await CollectDailyAsync(scopes, tome, query.ProjectId, cancellationToken);
            dailyGroups = daily.Groups;
            dailyIds = daily.Ids;
            dailyMore = daily.More;
            dailyFound = daily.Found;

            if (daily.Truncated || daily.More > 0)
            {
                omitted.Add(BriefSection.Daily);
            }
        }

        var journal = await CollectJournalAsync(query, scopes, tome, dailyIds, cancellationToken);
        var journalState = Fit(BriefSection.Journal, journal.Sum(entry => entry.Content.Length));

        var tomes = await CollectTomesAsync(cancellationToken);
        var tomesState = tomes.Degraded
            ? BriefSectionState.Empty
            : Fit(BriefSection.Tomes, tomes.Items.Sum(row => row.Key.Length + row.Description.Length + row.CreatedBy.Length));

        return new BriefResult
        {
            PersonaState = personaState,
            Persona = personaState == BriefSectionState.Populated ? document : null,
            FactsState = factsState,
            Facts = factsState == BriefSectionState.Populated ? facts : [],
            TombstonesState = tombstonesState,
            Tombstones = tombstonesState == BriefSectionState.Populated ? tombstones : [],
            JournalState = journalState,
            Journal = journalState == BriefSectionState.Populated ? journal : [],
            TomesState = tomesState,
            Tomes = tomesState == BriefSectionState.Populated ? tomes.Items : [],
            TomesMore = tomesState == BriefSectionState.Populated ? tomes.More : 0,
            TomesNote = tomes.Degraded ? tomes.Note : null,
            DailyState = dailyFound ? BriefSectionState.Populated : BriefSectionState.Empty,
            DailyGroups = dailyGroups,
            DailyMore = dailyMore,
            Omitted = omitted,
            CompactionDue = await compactionDue,
        };
    }

    private async Task<BriefCompactionDue?> MeasureCompactionDueAsync(string tome, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var filter = new MemoryFilter
        {
            Scopes = MemoryScopes.LongTerm,
            Kind = MemoryKind.Journal,
            Status = MemoryStatus.Active,
            Tome = tome,
        };

        var journal = await store.ListAsync(filter, cancellationToken);
        var staleThreshold = TimeSpan.FromDays(_options.Compaction.StalenessDays);
        if (journal.Count(record => now - record.LastAccessedAt >= staleThreshold) < _options.Compaction.MinClusterSize)
        {
            return null;
        }

        var points = await store.FetchAsync(filter, cancellationToken);
        var groups = CompactionPlanner.Plan(points, now, _options.Compaction);
        return groups.Count == 0
            ? null
            : new BriefCompactionDue { PendingEntries = groups.Sum(group => group.MemoryIds.Count) };
    }

    private async Task<IReadOnlyList<BriefFact>> CollectFactsAsync(
        BriefQuery query,
        IReadOnlyList<MemoryScope> scopes,
        string tome,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var records = await store.ListAsync(new MemoryFilter
        {
            Scopes = scopes,
            Kind = MemoryKind.Fact,
            Status = MemoryStatus.Active,
            Tome = tome,
            ProjectId = query.ProjectId,
            ExcludeTags = [PersonaSectionExtensions.PersonaTag],
        }, cancellationToken);

        var limit = ClampHint(query.FactsPerScope, DefaultFactsPerScope);
        return records
            .Select(record => (Record: record, Score: MemoryScorer.Score(NoSimilarity, record, now, _options.RecencyHalfLives)))
            .OrderByDescending(pair => pair.Score)
            .ThenByDescending(pair => pair.Record.CreatedAt)
            .ThenBy(pair => pair.Record.Id)
            .GroupBy(pair => pair.Record.Scope)
            .SelectMany(group => group.Take(limit))
            .OrderByDescending(pair => pair.Score)
            .ThenBy(pair => pair.Record.Id)
            .Select(pair => new BriefFact
            {
                Id = pair.Record.Id,
                Content = pair.Record.Content,
                Scope = pair.Record.Scope,
                Score = pair.Score,
                CreatedAt = pair.Record.CreatedAt,
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<BriefTombstone>> CollectTombstonesAsync(
        BriefQuery query,
        IReadOnlyList<MemoryScope> scopes,
        string tome,
        CancellationToken cancellationToken)
    {
        var records = await store.ListAsync(new MemoryFilter
        {
            Scopes = scopes,
            Status = MemoryStatus.Tombstoned,
            Tome = tome,
            ProjectId = query.ProjectId,
        }, cancellationToken);

        var limit = ClampHint(query.TombstoneLimit, DefaultTombstoneLimit);
        return records
            .OrderByDescending(record => record.TombstonedAt ?? DateTimeOffset.MinValue)
            .ThenBy(record => record.Id)
            .Take(limit)
            .Select(record => new BriefTombstone
            {
                Id = record.Id,
                Reason = record.TombstoneReason,
                TombstonedAt = record.TombstonedAt ?? DateTimeOffset.MinValue,
            })
            .ToArray();
    }

    private async Task<(IReadOnlyList<BriefDailyGroup> Groups, IReadOnlySet<Guid> Ids, int More, bool Truncated, bool Found)>
        CollectDailyAsync(
            IReadOnlyList<MemoryScope> scopes,
            string tome,
            string? projectId,
            CancellationToken cancellationToken)
    {
        var plan = new DailyWindowResolver(_options, clock).Resolve();
        var today = await ReadDailyWindowAsync(plan.Today, scopes, tome, projectId, cancellationToken);
        var (lastWorkingDay, lastWorkingDayRecords) =
            await FirstCandidateHoldingAsync(plan, scopes, tome, projectId, cancellationToken);

        var found = lastWorkingDayRecords.Concat(today).ToList();
        if (found.Count == 0)
        {
            return ([], new HashSet<Guid>(), 0, false, false);
        }

        var buckets = new List<(BriefDailyBucket Bucket, DateOnly Day, IReadOnlyList<MemoryRecord> Records)>();
        if (lastWorkingDay is { } window)
        {
            buckets.Add((BriefDailyBucket.LastWorkingDay, window.Day, lastWorkingDayRecords));
        }

        buckets.Add((BriefDailyBucket.Today, plan.Today.Day, today));

        var shaped = ShapeGroups(buckets);
        return (shaped.Groups, found.Select(record => record.Id).ToHashSet(), shaped.More, shaped.Truncated, true);
    }

    private (IReadOnlyList<BriefDailyGroup> Groups, int More, bool Truncated) ShapeGroups(
        List<(BriefDailyBucket Bucket, DateOnly Day, IReadOnlyList<MemoryRecord> Records)> buckets)
    {
        var entryCap = Math.Max(0, _options.BriefDailyMaxEntryChars);
        var totalCap = Math.Max(0, _options.BriefDailyMaxTotalChars);
        var groups = new List<BriefDailyGroup>();
        var spend = 0;
        var more = 0;
        var truncated = false;
        var exhausted = false;

        foreach (var bucket in buckets)
        {
            var ordered = bucket.Records
                .OrderBy(record => record.CreatedAt)
                .ThenBy(record => record.Id)
                .GroupBy(record => record.Actor.Alias, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var byActor in ordered)
            {
                var entries = new List<BriefDailyEntry>();
                foreach (var record in byActor)
                {
                    if (exhausted)
                    {
                        more++;
                        continue;
                    }

                    var capped = record.Content.Length > entryCap;
                    var content = capped ? record.Content[..entryCap] : record.Content;
                    if (spend + content.Length > totalCap)
                    {
                        exhausted = true;
                        more++;
                        continue;
                    }

                    spend += content.Length;
                    truncated |= capped;
                    entries.Add(new BriefDailyEntry
                    {
                        Id = record.Id,
                        Content = content,
                        Truncated = capped,
                        CreatedAt = record.CreatedAt,
                    });
                }

                if (entries.Count > 0)
                {
                    groups.Add(new BriefDailyGroup
                    {
                        Day = bucket.Day,
                        Bucket = bucket.Bucket,
                        Actor = byActor.Key,
                        Entries = entries,
                    });
                }
            }
        }

        return (groups, more, truncated);
    }

    private async Task<(DailyWindow? Window, IReadOnlyList<MemoryRecord> Records)> FirstCandidateHoldingAsync(
        DailyWindowPlan plan,
        IReadOnlyList<MemoryScope> scopes,
        string tome,
        string? projectId,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in plan.LastWorkingDayCandidates)
        {
            var records = await ReadDailyWindowAsync(candidate, scopes, tome, projectId, cancellationToken);
            if (records.Count > 0)
            {
                return (candidate, records);
            }
        }

        return (null, []);
    }

    private async Task<IReadOnlyList<MemoryRecord>> ReadDailyWindowAsync(
        DailyWindow window,
        IReadOnlyList<MemoryScope> scopes,
        string tome,
        string? projectId,
        CancellationToken cancellationToken)
    {
        var filter = new MemoryFilter
        {
            Scopes = scopes,
            Kind = MemoryKind.Journal,
            Status = MemoryStatus.Active,
            Tome = tome,
            ProjectId = projectId,
            Tags = [_options.DailyTag],
            CreatedAfter = window.CreatedAfter,
            CreatedBefore = window.CreatedBefore,
        };

        // The store cannot enforce CreatedBefore: MemoryFilterTranslator has no branch for it and
        // QdrantMemoryStore does not post-filter, so an upper bound sent here would be answered as
        // "everything at or after CreatedAfter". Every other bounded reader in the codebase
        // (MemoryService.ListAsync, DigestService) re-checks with Matches for exactly this reason.
        var records = await store.ListAsync(filter, cancellationToken);
        return [.. records.Where(record => filter.Matches(record))];
    }

    private async Task<IReadOnlyList<BriefJournalEntry>> CollectJournalAsync(
        BriefQuery query,
        IReadOnlyList<MemoryScope> scopes,
        string tome,
        IReadOnlySet<Guid>? excludeIds,
        CancellationToken cancellationToken)
    {
        var records = await store.ListAsync(new MemoryFilter
        {
            Scopes = scopes,
            Kind = MemoryKind.Journal,
            Status = MemoryStatus.Active,
            Tome = tome,
            ProjectId = query.ProjectId,
        }, cancellationToken);

        var limit = ClampHint(query.JournalPerScope, DefaultJournalPerScope);
        return records
            .Where(record => excludeIds is null || !excludeIds.Contains(record.Id))
            .OrderByDescending(record => record.CreatedAt)
            .ThenBy(record => record.Id)
            .GroupBy(record => record.Scope)
            .SelectMany(group => group.Take(limit))
            .OrderByDescending(record => record.CreatedAt)
            .ThenBy(record => record.Id)
            .Select(CapEntry)
            .ToArray();
    }

    private async Task<(bool Degraded, string? Note, IReadOnlyList<BriefTome> Items, int More)> CollectTomesAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TomeManifest> held;
        try
        {
            held = await manifests.ListAsync(cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return (true, $"tome manifests are unavailable: {failure.Message}", [], 0);
        }

        if (manifests.DegradationReason is { } reason)
        {
            return (true, reason, [], 0);
        }

        if (held.Count == 0)
        {
            return (false, null, [], 0);
        }

        var ordered = held.OrderBy(manifest => manifest.Key, StringComparer.Ordinal).ToArray();
        return (false, null, ordered
            .Take(TomeCap)
            .Select(manifest => new BriefTome
            {
                Key = manifest.Key,
                Description = manifest.Description,
                CreatedBy = manifest.CreatedBy,
            })
            .ToArray(), Math.Max(0, ordered.Length - TomeCap));
    }

    private BriefJournalEntry CapEntry(MemoryRecord record)
    {
        var truncated = record.Content.Length > _options.BriefMaxEntryChars;
        return new BriefJournalEntry
        {
            Id = record.Id,
            Content = truncated ? record.Content[.._options.BriefMaxEntryChars] : record.Content,
            Truncated = truncated,
            Scope = record.Scope,
            CreatedAt = record.CreatedAt,
        };
    }

    private static IReadOnlyList<MemoryScope> NormalizeScopes(IReadOnlyList<MemoryScope> scopes)
    {
        var longTerm = scopes
            .Where(scope => scope != MemoryScope.Session)
            .Distinct()
            .ToArray();

        return longTerm.Length > 0 ? longTerm : [MemoryScope.Project, MemoryScope.Global];
    }

    private string ResolveTome(string? tome) => TomeKey.Normalize(tome ?? _options.DefaultTome);

    private static int PersonaChars(PersonaDocument document) =>
        document.Identity.Length + document.Tone.Length + document.Preferences.Length + document.Rules.Length;

    private static int ClampHint(int? hint, int fallback) =>
        hint is null ? fallback : Math.Clamp(hint.Value, 1, LimitHintCeiling);
}
