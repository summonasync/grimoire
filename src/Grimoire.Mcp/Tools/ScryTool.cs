using System.ComponentModel;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class ScryTool(MemoryService memories, IMemoryStore store, MemoryActorProvider actors)
{
    public const string Name = "memory_scry";

    public const string Title = "Scry other tomes";

    public const string Description =
        "Read into OTHER tomes - the one deliberate cross-tome surface. Use it when your own tome lacks the " +
        "answer: a fresh tome needs the project history another agent already recorded, the human asks what a " +
        "sibling agent knows, or a decision might live in another project's tome. Ranking, query and limit " +
        "behave exactly like memory_recall over active records; superseded and tombstoned entries never surface. " +
        "Facts are searched by default, includeJournal opts into append-style journal entries, and session " +
        "memories never cross tomes. Results are anonymous by default - attribution 'light' adds the author " +
        "alias, 'full' adds alias and vendor; the store always keeps full provenance, anonymity is presentation " +
        "only. Without tomes every other tome is searched; pass tomes to narrow. Tome keys must be kebab-case; an " +
        "empty result means the other tomes hold nothing on the topic, not a failure. Scry is read-only and never " +
        "writes into your tome.";

    public Task<CallToolResult> Scry(
        [Description("What to look for. Phrasing may be approximate - scry is semantic, like recall.")]
        string query,
        [Description("Tomes to search; omit to search every tome except yours. Each entry must be a kebab-case tome key.")]
        string[]? tomes = null,
        [Description("Opt into other tomes' journal entries; facts alone are searched by default.")]
        bool includeJournal = false,
        [Description("none strips actor fields, light keeps the alias, full keeps alias and vendor. Default none.")]
        ScryAttribution attribution = ScryAttribution.None,
        [Description("Maximum matches to return, default 5.")]
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
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
            var own = await actors.ResolveTomeAsync(null, ct);
            var targets = tomes is { Length: > 0 }
                ? [.. tomes.Select(TomeKey.Normalize).Distinct().OrderBy(tome => tome, StringComparer.Ordinal)]
                : (await DiscoverTomesAsync(ct)).Where(tome => tome != own).ToArray();

            if (targets.Length == 0)
            {
                return ToolResults.Success(new ScryView
                {
                    Tomes = [],
                    Attribution = attribution,
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

            return ToolResults.Success(new ScryView
            {
                Tomes = targets,
                Attribution = attribution,
                Hits = [.. matches.Select(match => ToHit(match, attribution))],
                Count = matches.Count,
            });
        }, cancellationToken);
    }

    private async Task<IReadOnlyList<string>> DiscoverTomesAsync(CancellationToken cancellationToken)
    {
        var records = await store.ListAsync(new MemoryFilter { Status = MemoryStatus.Active }, cancellationToken);
        return [.. records.Select(record => record.Tome).Distinct().OrderBy(tome => tome, StringComparer.Ordinal)];
    }

    private static ScryHit ToHit(RecallMatch match, ScryAttribution attribution)
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
            ProjectId = match.Record.ProjectId,
            Similarity = match.Similarity,
            Score = match.Score,
            CreatedAt = match.Record.CreatedAt,
        };
    }
}
