using System.Diagnostics;
using Grimoire.Api.Contracts;
using Grimoire.Api.Tomes;
using Grimoire.Core;

namespace Grimoire.Api.Querying;

public sealed class ScryQueryRunner(MemoryService memories, IMemoryStore store, ApiTomeResolver tomes)
{
    public async Task<ScryResponse> RunAsync(ParsedScryQuery query, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var own = await tomes.ResolveAsync(null, cancellationToken);
        var targets = query.Tomes.Count > 0
            ? query.Tomes
            : [.. (await DiscoverTomesAsync(cancellationToken)).Where(tome => tome != own)];

        IReadOnlyList<RecallMatch> matches = targets.Count == 0
            ? []
            : (await memories.RecallAsync(new RecallQuery
            {
                Text = query.Query,
                Scopes = MemoryScopes.LongTerm,
                Kind = query.IncludeJournal ? null : MemoryKind.Fact,
                Tomes = targets,
                Limit = query.Limit,
            }, cancellationToken)).Matches;

        var terms = LexicalTermMatcher.Tokenize(query.Query);

        return new ScryResponse(
            targets,
            query.Attribution,
            matches.Count,
            [.. matches.Select(match => ToItem(match, query.Attribution, terms))],
            (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    private async Task<IReadOnlyList<string>> DiscoverTomesAsync(CancellationToken cancellationToken)
    {
        var records = await store.ListAsync(new MemoryFilter { Status = MemoryStatus.Active }, cancellationToken);
        return [.. records.Select(record => record.Tome).Distinct().OrderBy(tome => tome, StringComparer.Ordinal)];
    }

    private static ScryItem ToItem(RecallMatch match, ScryAttribution attribution, IReadOnlyList<string> terms) => new()
    {
        MemoryId = match.Record.Id,
        Content = match.Record.Content,
        Tome = match.Record.Tome,
        Scope = match.Record.Scope,
        Kind = match.Record.Kind,
        Actor = attribution switch
        {
            ScryAttribution.Light => match.Record.Actor.Alias,
            ScryAttribution.Full => match.Record.Actor.ToString(),
            _ => null,
        },
        Tags = match.Record.Tags,
        MatchedTerms = LexicalTermMatcher.Match(terms, match.Record),
        ProjectId = match.Record.ProjectId,
        Similarity = match.Similarity,
        Score = match.Score,
        CreatedAt = match.Record.CreatedAt,
    };
}
