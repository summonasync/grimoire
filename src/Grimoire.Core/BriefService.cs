namespace Grimoire.Core;

public sealed class BriefService(
    IMemoryStore store,
    PersonaService persona,
    IClock clock,
    MemoryEngineOptions? options = null)
{
    private const double NoSimilarity = 1.0;

    private const int LimitHintCeiling = 50;

    private const int DefaultFactsPerScope = 5;

    private const int DefaultJournalPerScope = 5;

    private const int DefaultTombstoneLimit = 10;

    private readonly MemoryEngineOptions _options = options ?? new();

    public async Task<BriefResult> BriefAsync(BriefQuery query, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var tome = ResolveTome(query.Tome);
        var scopes = NormalizeScopes(query.Scopes);
        var omitted = new List<BriefSection>();
        var remaining = _options.BriefMaxTotalChars;

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

        var journal = await CollectJournalAsync(query, scopes, tome, cancellationToken);
        var journalState = Fit(BriefSection.Journal, journal.Sum(entry => entry.Content.Length));

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
            Omitted = omitted,
        };
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

    private async Task<IReadOnlyList<BriefJournalEntry>> CollectJournalAsync(
        BriefQuery query,
        IReadOnlyList<MemoryScope> scopes,
        string tome,
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
            .OrderByDescending(record => record.CreatedAt)
            .ThenBy(record => record.Id)
            .GroupBy(record => record.Scope)
            .SelectMany(group => group.Take(limit))
            .OrderByDescending(record => record.CreatedAt)
            .ThenBy(record => record.Id)
            .Select(CapEntry)
            .ToArray();
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
