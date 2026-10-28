namespace Grimoire.Core;

public sealed class DigestService(
    IMemoryStore store,
    IEmbeddingProvider embeddings,
    IClock clock,
    MemoryEngineOptions? options = null)
{
    private const int LimitHintCeiling = 50;

    private const int FactCandidateLimit = 50;

    private readonly MemoryEngineOptions _options = options ?? new();

    public async Task<DigestResult> DigestAsync(DigestQuery query, CancellationToken cancellationToken)
    {
        var topic = ITopicClassifier.Normalize(query.Topic)
            ?? throw new ArgumentException("Digest topic must not be empty.", nameof(query));
        var now = clock.UtcNow;
        var tome = TomeKey.Normalize(query.Tome ?? _options.DefaultTome);
        var vector = await embeddings.EmbedAsync(TopicText(topic), cancellationToken);

        var omitted = new List<DigestSection>();
        var remaining = _options.DigestMaxTotalChars;

        (IReadOnlyList<TopicHit> Items, BriefSectionState State) Fit(DigestSection section, IReadOnlyList<TopicHit> hits)
        {
            var cost = hits.Sum(hit => Math.Min(hit.Record.Content.Length, _options.DigestMaxEntryChars));
            if (cost <= 0)
            {
                return ([], BriefSectionState.Empty);
            }

            if (cost > remaining)
            {
                omitted.Add(section);
                return ([], BriefSectionState.Omitted);
            }

            remaining -= cost;
            return (hits, BriefSectionState.Populated);
        }

        var longMemoHits = await ResolveTopicHitsAsync(
            SectionFilter(tome) with
            {
                Kind = MemoryKind.Fact,
                Status = MemoryStatus.Active,
            },
            vector,
            topic,
            FactCandidateLimit,
            cancellationToken);
        var (longMemoTopic, longMemoState) = Fit(DigestSection.LongMemo, longMemoHits);

        var recentCap = ClampHint(query.Limit, _options.DigestRecentCap);
        var recentHits = (await ResolveTopicHitsAsync(
                SectionFilter(tome, query.CreatedAfter, query.CreatedBefore) with
                {
                    Kind = MemoryKind.Journal,
                    Status = MemoryStatus.Active,
                },
                vector,
                topic,
                recentCap,
                cancellationToken))
            .Take(recentCap)
            .ToArray();
        var (recentTopic, recentState) = Fit(DigestSection.Recent, recentHits);

        var goneCap = ClampHint(query.Limit, _options.DigestGoneCap);
        var goneHits = (await ResolveTopicHitsAsync(
                SectionFilter(tome) with
                {
                    Statuses = [MemoryStatus.Superseded, MemoryStatus.Tombstoned],
                },
                vector,
                topic,
                goneCap,
                cancellationToken))
            .OrderByDescending(hit => hit.Record.TombstonedAt ?? hit.Record.CreatedAt)
            .ThenBy(hit => hit.Record.Id)
            .Take(goneCap)
            .ToArray();
        var (goneTopic, goneState) = Fit(DigestSection.Gone, goneHits);

        if (longMemoState == BriefSectionState.Populated)
        {
            longMemoTopic = await BumpAccessedAsync(longMemoTopic, now, cancellationToken);
        }

        if (recentState == BriefSectionState.Populated)
        {
            recentTopic = await BumpAccessedAsync(recentTopic, now, cancellationToken);
        }

        if (goneState == BriefSectionState.Populated)
        {
            goneTopic = await BumpAccessedAsync(goneTopic, now, cancellationToken);
        }

        return new DigestResult
        {
            Topic = topic,
            LongMemoState = longMemoState,
            LongMemo = [.. longMemoTopic.Select(ToHit)],
            RecentState = recentState,
            Recent = [.. recentTopic.Select(ToHit)],
            GoneState = goneState,
            Gone = [.. goneTopic.Select(ToHit)],
            Omitted = omitted,
        };
    }

    private sealed record TopicHit(MemoryRecord Record, DigestMatch Match, double? Similarity);

    private async Task<IReadOnlyList<TopicHit>> ResolveTopicHitsAsync(
        MemoryFilter filter,
        ReadOnlyMemory<float> vector,
        string topicTag,
        int candidateLimit,
        CancellationToken cancellationToken)
    {
        var hits = new Dictionary<Guid, TopicHit>();
        var tagged = await store.ListAsync(filter with { Tags = [topicTag] }, cancellationToken);
        foreach (var record in tagged)
        {
            hits[record.Id] = new TopicHit(record, DigestMatch.Tag, null);
        }

        var similar = await store.SearchAsync(vector, filter, candidateLimit, cancellationToken);
        foreach (var match in similar)
        {
            if (match.Similarity < _options.DigestSimilarityThreshold)
            {
                continue;
            }

            hits.TryAdd(match.Record.Id, new TopicHit(match.Record, DigestMatch.Similar, match.Similarity));
        }

        return hits.Values
            .Where(hit => filter.Matches(hit.Record))
            .OrderByDescending(hit => hit.Record.CreatedAt)
            .ThenBy(hit => hit.Record.Id)
            .ToArray();
    }

    private async Task<IReadOnlyList<TopicHit>> BumpAccessedAsync(
        IReadOnlyList<TopicHit> hits,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var bumped = new List<TopicHit>(hits.Count);
        foreach (var hit in hits)
        {
            var accessed = hit.Record with
            {
                AccessCount = hit.Record.AccessCount + 1,
                LastAccessedAt = now,
            };

            await store.UpdateAsync(accessed, cancellationToken);
            bumped.Add(hit with { Record = accessed });
        }

        return bumped;
    }

    private DigestHit ToHit(TopicHit hit)
    {
        var truncated = hit.Record.Content.Length > _options.DigestMaxEntryChars;
        return new DigestHit
        {
            Id = hit.Record.Id,
            Content = truncated ? hit.Record.Content[.._options.DigestMaxEntryChars] : hit.Record.Content,
            Match = hit.Match,
            Similarity = hit.Similarity,
            Scope = hit.Record.Scope,
            Kind = hit.Record.Kind,
            Tags = hit.Record.Tags,
            TombstoneReason = hit.Record.TombstoneReason,
            Truncated = truncated,
            CreatedAt = hit.Record.CreatedAt,
            LastAccessedAt = hit.Record.LastAccessedAt,
        };
    }

    private MemoryFilter SectionFilter(
        string tome,
        DateTimeOffset? createdAfter = null,
        DateTimeOffset? createdBefore = null) => new()
        {
            Scopes = MemoryScopes.LongTerm,
            Tome = tome,
            CreatedAfter = createdAfter,
            CreatedBefore = createdBefore,
            ExcludeTags = [PersonaSectionExtensions.PersonaTag],
        };

    private static string TopicText(string topicTag) => topicTag[ITopicClassifier.TopicPrefix.Length..];

    private int ClampHint(int? hint, int fallback) =>
        hint is null ? fallback : Math.Clamp(hint.Value, 1, LimitHintCeiling);
}
