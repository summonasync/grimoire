using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;
namespace Grimoire.Mcp.Tools;

internal sealed class RecallTool(
    MemoryService memories,
    IMemoryStore store,
    MemoryActorProvider actors,
    IProjectRegistry projects,
    ICallerPathProvider callerPaths)
{
    public const string Name = "memory_recall";

    public const string Title = "Recall memories";

    public const string Description =
        "Search memory before you assume - ranked semantic matches over the active records of one tome; " +
        "superseded and tombstoned never surface, and a read bumps the record's access stats. Call it when prior " +
        "context could change the answer; narrow with scope, projectId, tags or the since/until/actor filters " +
        "before raising limit, and pass tome to read another tome you own, memory_scry answers across tomes. " +
        "Pass fields or compact to trim hits to the keys you need - the cheap path for recurring reads.";

    private static readonly ReadProjectionSurface Surface = new()
    {
        AllowedKeys =
        [
            "content", "scope", "kind", "actor", "tags", "projectId", "sessionId",
            "createdAt", "lastAccessedAt", "similarity",
        ],
        AlwaysOnKeys = ["memoryId", "score"],
        CompactKeys = ["memoryId", "content", "tags", "createdAt"],
    };

    public Task<CallToolResult> Recall(
        [Description("What to look for. Phrasing may be approximate - recall is semantic, not keyword match.")]
        string query,
        [Description("Restrict to one scope; omit to search all long-term scopes (Project, Global, Personal).")]
        [AllowedValues("Session", "Project", "Global", "Personal")]
        string? scope = null,
        [Description("Restrict to Journal or Fact records.")]
        [AllowedValues("Journal", "Fact")]
        string? kind = null,
        [Description("Restrict to memories of one project.")]
        string? projectId = null,
        [Description("Restrict to one session's memories.")]
        string? sessionId = null,
        [Description("Restrict to memories carrying any of these tags.")]
        string[]? tags = null,
        [Description("Only records created at or after this; yyyy-MM-dd means local midnight, an ISO-8601 instant is used as-is.")]
        string? since = null,
        [Description("Only records created before this; exclusive; yyyy-MM-dd means local midnight, an ISO-8601 instant is used as-is.")]
        string? until = null,
        [Description("Only records written by this actor: an alias alone matches any vendor, alias+vendor pins one exactly.")]
        string? actor = null,
        [Description("Maximum matches to return, default 5.")]
        int limit = 5,
        [Description("Tome to search; omit for your resolved tome, or name another tome you own to read it. The response echoes the tome actually searched. For cross-tome context without leaving your tome use memory_scry.")]
        string? tome = null,
        [Description("Project each hit to these camelCase keys, e.g. [\"content\", \"similarity\"]; memoryId and score are always included and an unknown field is rejected naming the allowed set.")]
        string[]? fields = null,
        [Description("Project each hit to the digest shape (memoryId, content, tags, createdAt) plus score - the cheap path for recurring reads; mutually exclusive with fields.")]
        bool compact = false,
        CancellationToken cancellationToken = default)
    {
        var (scopeFilter, scopeError) = EnumParams.Parse<MemoryScope>(scope, nameof(scope));
        if (scopeError is not null)
        {
            return Task.FromResult(ToolResults.Failure(scopeError));
        }

        var (kindFilter, kindError) = EnumParams.Parse<MemoryKind>(kind, nameof(kind));
        if (kindError is not null)
        {
            return Task.FromResult(ToolResults.Failure(kindError));
        }

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

        var (range, rangeError) = DateParams.Parse(since, until);
        if (rangeError is not null)
        {
            return Task.FromResult(ToolResults.Failure(rangeError));
        }

        if (TomeParams.Validate(tome, nameof(tome)) is { } tomeError)
        {
            return Task.FromResult(ToolResults.Failure(tomeError));
        }

        IReadOnlyList<string>? projectedKeys;
        try
        {
            projectedKeys = ReadProjection.Resolve(fields, compact, Surface);
        }
        catch (ReadProjectionValidationException projectionError)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                projectionError.Message,
                nameof(fields))));
        }

        return ToolResults.ExecuteAsync(async ct =>
        {
            var resolvedTome = await actors.ResolveTomeAsync(tome, ct);
            var recall = await memories.RecallAsync(new RecallQuery
            {
                Text = query,
                Scopes = scopeFilter is null ? [] : [scopeFilter.Value],
                Kind = kindFilter,
                Tags = tags ?? [],
                Tome = resolvedTome,
                ProjectId = projectId,
                CallerProjectId = projects.ResolveByPath(callerPaths.Path)?.Alias,
                SessionId = sessionId,
                CreatedAfter = range.Since,
                CreatedBefore = range.Until,
                Actor = actor,
                Limit = limit,
            }, ct);
            var matches = recall.Matches;

            var warnings = WarningMerge.Merge(
                recall.Warnings,
                await EmptyTomeWarning.BuildAsync(store, matches.Count == 0, tome, resolvedTome, ct));

            if (projectedKeys is null)
            {
                return ToolResults.Success(new RecallView
                {
                    Tome = resolvedTome,
                    Matches = [.. matches.Select(ToHit)],
                    Count = matches.Count,
                    Warnings = warnings,
                });
            }

            return ToolResults.Success(new ProjectedRecallView
            {
                Tome = resolvedTome,
                Matches = [.. matches.Select(match => ProjectHit(ToHit(match), projectedKeys))],
                Count = matches.Count,
                Warnings = warnings,
            });
        }, cancellationToken);
    }

    private static RecallHit ToHit(RecallMatch match)
    {
        return new RecallHit
        {
            MemoryId = match.Record.Id,
            Content = match.Record.Content,
            Scope = match.Record.Scope,
            Kind = match.Record.Kind,
            Actor = match.Record.Actor.ToString(),
            Tags = match.Record.Tags,
            ProjectId = match.Record.ProjectId,
            SessionId = match.Record.SessionId,
            Similarity = match.Similarity,
            Score = match.Score,
            CreatedAt = match.Record.CreatedAt,
            LastAccessedAt = match.Record.LastAccessedAt,
        };
    }

    private static IReadOnlyDictionary<string, object?> ProjectHit(RecallHit hit, IReadOnlyList<string> keys)
    {
        var projected = new Dictionary<string, object?>(keys.Count, StringComparer.Ordinal);
        foreach (var key in keys)
        {
            projected[key] = key switch
            {
                "memoryId" => hit.MemoryId,
                "content" => hit.Content,
                "scope" => hit.Scope,
                "kind" => hit.Kind,
                "actor" => hit.Actor,
                "tags" => hit.Tags,
                "projectId" => hit.ProjectId,
                "sessionId" => hit.SessionId,
                "similarity" => hit.Similarity,
                "score" => hit.Score,
                "createdAt" => hit.CreatedAt,
                "lastAccessedAt" => hit.LastAccessedAt,
                _ => throw new UnreachableException($"key '{key}' passed validation but has no projection."),
            };
        }

        return projected;
    }
}
