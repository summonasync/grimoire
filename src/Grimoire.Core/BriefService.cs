namespace Grimoire.Core;

public sealed class BriefService(
    IMemoryStore store,
    PersonaService persona,
    ITomeManifestStore manifests,
    IClock clock,
    MemoryEngineOptions? options = null)
{
    private const double NoSimilarity = 1.0;

    private const double NoScore = 0;

    private const int NoGroup = 0;

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

        // The collect paths hand back what their limits trimmed and the purse dropped. Together with the
        // records of any section that fell out of budget those remainders become the omission digest —
        // records that were never in the running (outside the caller's window, already shown as daily
        // notes, or in buckets the window emptied) are excluded, not lost, and stay out of the digest.
        var (facts, factsDropped) = await CollectFactsAsync(query, scopes, tome, now, cancellationToken);
        // Facts take whole entries while the purse holds instead of dropping the section wholesale:
        // a fresh session should meet its standing facts ahead of tombstones and journal, so a tight
        // budget starves the scored tail, never the whole section.
        var keptFacts = new List<BriefFact>(facts.Count);
        var budgetFacts = new List<BriefFact>();
        foreach (var fact in facts)
        {
            if (fact.Content.Length <= remaining)
            {
                keptFacts.Add(fact);
                remaining -= fact.Content.Length;
            }
            else
            {
                budgetFacts.Add(fact);
            }
        }

        var factsState = facts.Count == 0
            ? BriefSectionState.Empty
            : keptFacts.Count > 0 ? BriefSectionState.Populated : BriefSectionState.Omitted;
        if (factsState == BriefSectionState.Omitted)
        {
            omitted.Add(BriefSection.Facts);
        }

        var (tombstones, tombstonesDropped) = await CollectTombstonesAsync(query, scopes, tome, cancellationToken);
        var tombstonesState = Fit(BriefSection.Tombstones, tombstones.Sum(tombstone => tombstone.Reason?.Length ?? 0));

        IReadOnlyList<BriefDailyGroup> dailyGroups = [];
        IReadOnlySet<Guid>? dailyIds = null;
        var dailyMore = 0;
        var dailyFound = false;
        IReadOnlyList<string>? warnings = null;
        IReadOnlyList<BriefOmissionLine> dailyDropped = [];

        if (query.Daily)
        {
            var zone = DailyWindowResolver.ResolveZone(_options.DailyTimeZone);
            List<string> dailyWarnings = [];

            // Ordered by the config key each warning names: the disabled section first, then the zone
            // its buckets would have used. T5 prints this list as-is, so this order is the CLI's order.
            if (string.IsNullOrWhiteSpace(_options.DailyTag))
            {
                dailyWarnings.Add("DailyTag is empty; the daily section is disabled. Set DailyTag to enable it.");
            }

            if (zone.Source == DailyZoneSource.UtcFallback)
            {
                dailyWarnings.Add($"DailyTimeZone '{_options.DailyTimeZone}' did not resolve; daily buckets use UTC.");
            }

            if (dailyWarnings.Count > 0)
            {
                warnings = dailyWarnings;
            }

            var daily = await CollectDailyAsync(zone, scopes, tome, query, cancellationToken);
            dailyGroups = daily.Groups;
            dailyIds = daily.Ids;
            dailyMore = daily.More;
            dailyFound = daily.Found;
            dailyDropped = [.. daily.Dropped.Select(record => new BriefOmissionLine(
                record.Id,
                record.Content,
                DailyWindowResolver.LocalDay(record.CreatedAt, zone.Zone).DayNumber,
                NoScore,
                record.CreatedAt,
                BriefOmissionReason.DailyPurse,
                record.Actor.Alias))];

            if (daily.Truncated || daily.More > 0)
            {
                omitted.Add(BriefSection.Daily);
            }
        }

        var (journal, journalDropped) = await CollectJournalAsync(query, scopes, tome, dailyIds, cancellationToken);
        var journalState = Fit(BriefSection.Journal, journal.Sum(entry => entry.Content.Length));

        var tomes = await CollectTomesAsync(cancellationToken);
        var tomesState = tomes.Degraded
            ? BriefSectionState.Empty
            : Fit(BriefSection.Tomes, tomes.Items.Sum(row => row.Key.Length + row.Description.Length + row.CreatedBy.Length));

        // Persona and tomes can also fall out of budget, and they keep their existing faces: the section
        // name in `omitted` and, for tomes, `TomesMore`. Neither is a set of memory records with ids the
        // reader can recall by, so no digest line can name what fell out of them.
        var digest = BriefOmissionDigest.Build(
            FactOmissions(budgetFacts, factsDropped, scopes, now),
            TombstoneOmissions(tombstonesState, tombstones, tombstonesDropped),
            JournalOmissions(journalState, journal, journalDropped, scopes),
            dailyDropped,
            _options);

        return new BriefResult
        {
            PersonaState = personaState,
            Persona = personaState == BriefSectionState.Populated ? document : null,
            FactsState = factsState,
            Facts = factsState == BriefSectionState.Populated ? keptFacts : [],
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
            OmissionDigest = digest,
            CompactionDue = await compactionDue,
            Warnings = warnings,
        };
    }

    private IReadOnlyList<BriefOmissionLine> FactOmissions(
        IReadOnlyList<BriefFact> budgetDropped,
        IReadOnlyList<MemoryRecord> trimmed,
        IReadOnlyList<MemoryScope> scopes,
        DateTimeOffset now)
    {
        var lines = new List<BriefOmissionLine>(
            // the trimmed remainder comes back as bare records, so the digest re-scores them with the same
            // call the collect path used; a budget-dropped fact already carries its score
            from record in trimmed
            select new BriefOmissionLine(
                record.Id,
                record.Content,
                ScopeRank(scopes, record.Scope),
                MemoryScorer.Score(NoSimilarity, record, now, _options.RecencyHalfLives),
                record.CreatedAt,
                BriefOmissionReason.ReadLimit));

        // whatever the purse cut — the whole section or only the scored tail — is named the same way
        lines.AddRange(
            from fact in budgetDropped
            select new BriefOmissionLine(
                fact.Id,
                fact.Content,
                ScopeRank(scopes, fact.Scope),
                fact.Score,
                fact.CreatedAt,
                BriefOmissionReason.SectionBudget));

        return lines;
    }

    private static IReadOnlyList<BriefOmissionLine> TombstoneOmissions(
        BriefSectionState state,
        IReadOnlyList<BriefTombstone> kept,
        IReadOnlyList<MemoryRecord> trimmed)
    {
        var lines = new List<BriefOmissionLine>(
            from record in trimmed
            select new BriefOmissionLine(
                record.Id,
                record.TombstoneReason ?? record.Content,
                NoGroup,
                NoScore,
                record.TombstonedAt ?? DateTimeOffset.MinValue,
                BriefOmissionReason.ReadLimit));

        if (state == BriefSectionState.Omitted)
        {
            lines.AddRange(
                from tombstone in kept
                select new BriefOmissionLine(
                    tombstone.Id,
                    tombstone.Reason ?? string.Empty,
                    NoGroup,
                    NoScore,
                    tombstone.TombstonedAt,
                    BriefOmissionReason.SectionBudget));
        }

        return lines;
    }

    private static IReadOnlyList<BriefOmissionLine> JournalOmissions(
        BriefSectionState state,
        IReadOnlyList<BriefJournalEntry> kept,
        IReadOnlyList<MemoryRecord> trimmed,
        IReadOnlyList<MemoryScope> scopes)
    {
        var lines = new List<BriefOmissionLine>(
            from record in trimmed
            select new BriefOmissionLine(
                record.Id,
                record.Content,
                ScopeRank(scopes, record.Scope),
                NoScore,
                record.CreatedAt,
                BriefOmissionReason.ReadLimit));

        if (state == BriefSectionState.Omitted)
        {
            lines.AddRange(
                from entry in kept
                select new BriefOmissionLine(
                    entry.Id,
                    entry.Content,
                    ScopeRank(scopes, entry.Scope),
                    NoScore,
                    entry.CreatedAt,
                    BriefOmissionReason.SectionBudget));
        }

        return lines;
    }

    // The digest groups follow the scope list the read was asked for, so a reader meets the dropped
    // records in the order the sections below them are printed; scopes nobody asked for sort last.
    private static int ScopeRank(IReadOnlyList<MemoryScope> scopes, MemoryScope scope)
    {
        for (var rank = 0; rank < scopes.Count; rank++)
        {
            if (scopes[rank] == scope)
            {
                return rank;
            }
        }

        return scopes.Count + (int)scope;
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

    internal async Task<(IReadOnlyList<BriefFact> Items, IReadOnlyList<MemoryRecord> Dropped)> CollectFactsAsync(
        BriefQuery query,
        IReadOnlyList<MemoryScope> scopes,
        string tome,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var filter = new MemoryFilter
        {
            Scopes = scopes,
            Kind = MemoryKind.Fact,
            Status = MemoryStatus.Active,
            Tome = tome,
            ProjectId = query.ProjectId,
            ExcludeTags = [PersonaSectionExtensions.PersonaTag],
            CreatedAfter = query.Since,
            CreatedBefore = query.Until,
        };

        var records = await store.ListAsync(filter, cancellationToken);

        var limit = ClampHint(query.FactsPerScope, DefaultFactsPerScope);
        // Matches re-checks the window for the reason spelled out in ReadDailyWindowAsync.
        var byScope = records
            .Where(record => filter.Matches(record))
            .Select(record => (Record: record, Score: MemoryScorer.Score(NoSimilarity, record, now, _options.RecencyHalfLives)))
            .OrderByDescending(pair => pair.Score)
            .ThenByDescending(pair => pair.Record.CreatedAt)
            .ThenBy(pair => pair.Record.Id)
            .GroupBy(pair => pair.Record.Scope)
            .ToArray();

        var items = byScope
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

        var dropped = byScope
            .SelectMany(group => group.Skip(limit))
            .Select(pair => pair.Record)
            .ToArray();

        return (items, dropped);
    }

    internal async Task<(IReadOnlyList<BriefTombstone> Items, IReadOnlyList<MemoryRecord> Dropped)> CollectTombstonesAsync(
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
        var ranked = records
            .OrderByDescending(record => record.TombstonedAt ?? DateTimeOffset.MinValue)
            .ThenBy(record => record.Id)
            .ToArray();

        var items = ranked
            .Take(limit)
            .Select(record => new BriefTombstone
            {
                Id = record.Id,
                Reason = record.TombstoneReason,
                TombstonedAt = record.TombstonedAt ?? DateTimeOffset.MinValue,
            })
            .ToArray();

        return (items, [.. ranked.Skip(limit)]);
    }

    internal async Task<(
        IReadOnlyList<BriefDailyGroup> Groups,
        IReadOnlySet<Guid> Ids,
        int More,
        bool Truncated,
        bool Found,
        IReadOnlyList<MemoryRecord> Dropped)> CollectDailyAsync(
            DailyZoneResolution zone,
            IReadOnlyList<MemoryScope> scopes,
            string tome,
            BriefQuery query,
            CancellationToken cancellationToken)
    {
        var plan = new DailyWindowResolver(_options, clock).Resolve(zone.Zone);
        var today = await ReadDailyWindowAsync(plan.Today, scopes, tome, query, cancellationToken);
        var (lastWorkingDay, lastWorkingDayRecords) =
            await FirstCandidateHoldingAsync(plan, scopes, tome, query, cancellationToken);

        var found = lastWorkingDayRecords.Concat(today).ToList();
        if (found.Count == 0)
        {
            return ([], new HashSet<Guid>(), 0, false, false, []);
        }

        var buckets = new List<(BriefDailyBucket Bucket, DateOnly Day, IReadOnlyList<MemoryRecord> Records)>();
        if (lastWorkingDay is { } window)
        {
            buckets.Add((BriefDailyBucket.LastWorkingDay, window.Day, lastWorkingDayRecords));
        }

        buckets.Add((BriefDailyBucket.Today, plan.Today.Day, today));

        var shaped = ShapeGroups(buckets);
        return (shaped.Groups, found.Select(record => record.Id).ToHashSet(), shaped.More, shaped.Truncated, true, shaped.Dropped);
    }

    private (IReadOnlyList<BriefDailyGroup> Groups, int More, bool Truncated, IReadOnlyList<MemoryRecord> Dropped) ShapeGroups(
        List<(BriefDailyBucket Bucket, DateOnly Day, IReadOnlyList<MemoryRecord> Records)> buckets)
    {
        var entryCap = Math.Max(0, _options.BriefDailyMaxEntryChars);
        var totalCap = Math.Max(0, _options.BriefDailyMaxTotalChars);
        var groups = new List<BriefDailyGroup>();
        var spend = 0;
        var truncated = false;
        var exhausted = false;
        var dropped = new List<MemoryRecord>();

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
                        dropped.Add(record);
                        continue;
                    }

                    var capped = Cuts(record.Content.Length, entryCap);
                    var content = capped ? record.Content[..entryCap] : record.Content;
                    if (spend + content.Length > totalCap)
                    {
                        exhausted = true;
                        dropped.Add(record);
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

        return (groups, dropped.Count, truncated, dropped);
    }

    private async Task<(DailyWindow? Window, IReadOnlyList<MemoryRecord> Records)> FirstCandidateHoldingAsync(
        DailyWindowPlan plan,
        IReadOnlyList<MemoryScope> scopes,
        string tome,
        BriefQuery query,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in plan.LastWorkingDayCandidates)
        {
            var records = await ReadDailyWindowAsync(candidate, scopes, tome, query, cancellationToken);
            if (records.Count > 0)
            {
                return (candidate, records);
            }
        }

        return (null, []);
    }

    private async Task<IReadOnlyList<MemoryRecord>> ReadDailyWindowAsync(
        DailyWindow bucket,
        IReadOnlyList<MemoryScope> scopes,
        string tome,
        BriefQuery query,
        CancellationToken cancellationToken)
    {
        if (Narrow(bucket, query) is not { } window)
        {
            return [];
        }

        var filter = new MemoryFilter
        {
            Scopes = scopes,
            Kind = MemoryKind.Journal,
            Status = MemoryStatus.Active,
            Tome = tome,
            ProjectId = query.ProjectId,
            Tags = [_options.DailyTag],
            CreatedAfter = window.CreatedAfter,
            CreatedBefore = window.CreatedBefore,
        };

        // Two faces, two answers about this re-check. Qdrant has enforced CreatedBefore as an exclusive lt
        // bound since feature 055, so in production this is defensive. The in-memory fakes are not that
        // store: FakeMemoryStore.Matching applies CreatedAfter and never consults CreatedBefore, which makes
        // the line below load-bearing for every boundary test in Core and Cli. Deleting it keeps production
        // correct and reddens the suite — the root fix is teaching the doubles the bound, and that call is
        // feature 055's, not this one's.
        var records = await store.ListAsync(filter, cancellationToken);
        return [.. records.Where(record => filter.Matches(record))];
    }

    // The caller's window narrows a bucket instead of replacing it, so the half-open pair keeps its
    // exclusive upper edge. A bucket the window empties comes back null and is never read: no bucket read
    // sends a range whose lower bound sits on or after its upper one.
    private static DailyWindow? Narrow(DailyWindow bucket, BriefQuery query)
    {
        var after = query.Since is { } since && since > bucket.CreatedAfter ? since : bucket.CreatedAfter;
        var before = query.Until is { } until && until < bucket.CreatedBefore ? until : bucket.CreatedBefore;

        return after < before ? bucket with { CreatedAfter = after, CreatedBefore = before } : null;
    }

    internal async Task<(IReadOnlyList<BriefJournalEntry> Items, IReadOnlyList<MemoryRecord> Dropped)> CollectJournalAsync(
        BriefQuery query,
        IReadOnlyList<MemoryScope> scopes,
        string tome,
        IReadOnlySet<Guid>? excludeIds,
        CancellationToken cancellationToken)
    {
        var filter = new MemoryFilter
        {
            Scopes = scopes,
            Kind = MemoryKind.Journal,
            Status = MemoryStatus.Active,
            Tome = tome,
            ProjectId = query.ProjectId,
            CreatedAfter = query.Since,
            CreatedBefore = query.Until,
        };

        var records = await store.ListAsync(filter, cancellationToken);

        var limit = ClampHint(query.JournalPerScope, DefaultJournalPerScope);
        // Matches re-checks the window for the reason spelled out in ReadDailyWindowAsync.
        var byScope = records
            .Where(record => filter.Matches(record))
            .Where(record => excludeIds is null || !excludeIds.Contains(record.Id))
            .OrderByDescending(record => record.CreatedAt)
            .ThenBy(record => record.Id)
            .GroupBy(record => record.Scope)
            .ToArray();

        var items = byScope
            .SelectMany(group => group.Take(limit))
            .OrderByDescending(record => record.CreatedAt)
            .ThenBy(record => record.Id)
            .Select(CapEntry)
            .ToArray();

        var dropped = byScope
            .SelectMany(group => group.Skip(limit))
            .ToArray();

        return (items, dropped);
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
        var truncated = Cuts(record.Content.Length, _options.BriefMaxEntryChars);
        return new BriefJournalEntry
        {
            Id = record.Id,
            Content = truncated ? record.Content[.._options.BriefMaxEntryChars] : record.Content,
            Truncated = truncated,
            Scope = record.Scope,
            CreatedAt = record.CreatedAt,
        };
    }

    // The never-truncate floor is a guarantee, not a cap: a record at or under it stays whole no matter
    // how tight the section cap gets, so a long note is only ever cut when it is too long to be short.
    private bool Cuts(int length, int cap) =>
        length > cap && length > Math.Max(0, _options.BriefNeverTruncateChars);

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
