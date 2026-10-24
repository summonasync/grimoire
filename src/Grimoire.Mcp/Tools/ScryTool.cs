using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class ScryTool(MemoryService memories, IMemoryStore store, MemoryActorProvider actors)
{
    public const string Name = "memory_scry";

    public const string Title = "Scry other tomes";

    public const string Description =
        "Read into OTHER tomes - the one deliberate cross-tome surface; the only trace is an access bump on the " +
        "records it surfaces, never a new row in your tome. Facts alone unless includeJournal opts in; session " +
        "rows never cross, superseded and tombstoned never surface, and hits stay anonymous until attribution " +
        "lifts the provenance veil.";

    public Task<CallToolResult> Scry(
        [Description("What to look for. Phrasing may be approximate - scry is semantic, like recall.")]
        string query,
        [Description("Tomes to search; omit to search every tome except yours. Each entry must be a kebab-case tome key.")]
        string[]? tomes = null,
        [Description("Opt into other tomes' journal entries; facts alone are searched by default.")]
        bool includeJournal = false,
        [Description("None strips actor fields, Light keeps the alias, Full keeps alias and vendor. Default None.")]
        [AllowedValues("None", "Light", "Full")]
        string attribution = "None",
        [Description("Maximum matches to return, default 5.")]
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var (attributionValue, attributionError) = EnumParams.Parse<ScryAttribution>(attribution, nameof(attribution));
        if (attributionError is not null)
        {
            return Task.FromResult(ToolResults.Failure(attributionError));
        }

        var attributionFilter = attributionValue ?? ScryAttribution.None;

        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                "query must not be empty or whitespace.",
                nameof(query))));
        }

        if (limit <= 0)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                "limit must be a positive integer.",
                nameof(limit))));
        }

        if (TomeParams.Validate(tomes ?? [], nameof(tomes)) is { } tomeError)
        {
            return Task.FromResult(ToolResults.Failure(tomeError));
        }

        return ToolResults.ExecuteAsync(async ct =>
        {
            string[] targets;
            if (tomes is { Length: > 0 })
            {
                var requested = tomes.Select(TomeKey.Normalize).Distinct()
                    .OrderBy(tome => tome, StringComparer.Ordinal).ToArray();
                var known = await KnownTomeKeysAsync(ct);
                var unknown = requested.Where(tome => !known.Contains(tome)).ToArray();
                if (unknown.Length > 0)
                {
                    return ToolResults.Failure(UnknownTomeError(nameof(tomes), unknown, known));
                }

                targets = requested;
            }
            else
            {
                var own = await actors.ResolveTomeAsync(null, ct);
                targets = (await DiscoverTomesAsync(ct)).Where(tome => tome != own).ToArray();
            }

            if (targets.Length == 0)
            {
                return ToolResults.Success(new ScryView
                {
                    Tomes = [],
                    Attribution = attributionFilter,
                    Hits = [],
                    Count = 0,
                });
            }

            var matches = await memories.RecallAsync(new RecallQuery
            {
                Text = query,
                Scopes = MemoryScopes.LongTerm,
                Kind = includeJournal ? null : MemoryKind.Fact,
                Tomes = targets,
                Limit = limit,
            }, ct);

            var terms = LexicalTermMatcher.Tokenize(query);

            return ToolResults.Success(new ScryView
            {
                Tomes = targets,
                Attribution = attributionFilter,
                Hits = [.. matches.Select(match => ToHit(match, attributionFilter, terms))],
                Count = matches.Count,
            });
        }, cancellationToken);
    }

    private async Task<IReadOnlyList<string>> DiscoverTomesAsync(CancellationToken cancellationToken)
    {
        var records = await store.ListAsync(new MemoryFilter { Status = MemoryStatus.Active }, cancellationToken);
        return [.. records.Select(record => record.Tome).Distinct().OrderBy(tome => tome, StringComparer.Ordinal)];
    }

    private async Task<IReadOnlyList<string>> KnownTomeKeysAsync(CancellationToken cancellationToken)
    {
        var active = await store.ListAsync(new MemoryFilter { Status = MemoryStatus.Active }, cancellationToken);
        var tombstoned = await store.ListAsync(new MemoryFilter { Status = MemoryStatus.Tombstoned }, cancellationToken);
        return [.. active.Concat(tombstoned).Select(record => record.Tome).Distinct()
            .OrderBy(tome => tome, StringComparer.Ordinal)];
    }

    private static ToolError UnknownTomeError(string paramName, string[] unknown, IReadOnlyList<string> known)
    {
        var unknownList = string.Join(", ", unknown.Select(tome => $"'{tome}'"));
        var knownList = known.Count == 0 ? "(none yet)" : string.Join(", ", known);
        return new ToolError(
            ToolErrorCodes.InvalidRequest,
            $"{paramName}: {unknownList} is not a known tome. Known tome keys (the memory_stats.byTome key set): {knownList}.",
            paramName);
    }

    private static ScryHit ToHit(RecallMatch match, ScryAttribution attribution, IReadOnlyList<string> terms)
    {
        return new ScryHit
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
}
