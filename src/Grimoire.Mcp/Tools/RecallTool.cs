using System.ComponentModel;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;
namespace Grimoire.Mcp.Tools;

internal sealed class RecallTool(MemoryService memories, MemoryActorProvider actors)
{
    public const string Name = "memory_recall";

    public const string Title = "Recall memories";

    public const string Description =
        "Search memory before you assume. Call it at session start (project and global scope) to load standing " +
        "context, before redoing work that may already have a decision on record, and whenever prior context would " +
        "change the answer: \"did we settle X\", \"what did the human say about Y\", \"why is this built this way\". " +
        "Matches are ranked by similarity, recency and importance over active records, and reading them bumps their " +
        "access stats so live knowledge stays fresh. Narrow with scope, projectId or tags instead of raising the " +
        "limit, and pass tome to read another tome you own; memory_scry answers across tomes instead.";

    public Task<CallToolResult> Recall(
        [Description("What to look for. Phrasing may be approximate - recall is semantic, not keyword match.")]
        string query,
        [Description("Restrict to one scope; omit to search all long-term scopes (project, global, personal).")]
        MemoryScope? scope = null,
        [Description("Restrict to journal or fact records.")]
        MemoryKind? kind = null,
        [Description("Restrict to memories of one project.")]
        string? projectId = null,
        [Description("Restrict to one session's memories.")]
        string? sessionId = null,
        [Description("Restrict to memories carrying any of these tags.")]
        string[]? tags = null,
        [Description("Maximum matches to return, default 5.")]
        int limit = 5,
        [Description("Tome to search; omit for your resolved tome, or name another tome you own to read it. The response echoes the tome actually searched. For cross-tome context without leaving your tome use memory_scry.")]
        string? tome = null,
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

        if (TomeParams.Validate(tome, nameof(tome)) is { } tomeError)
        {
            return Task.FromResult(ToolResults.Failure(tomeError));
        }

        return ToolResults.ExecuteAsync(async ct =>
        {
            var resolvedTome = await actors.ResolveTomeAsync(tome, ct);
            var matches = await memories.RecallAsync(new RecallQuery
            {
                Text = query,
                Scopes = scope is null ? [] : [scope.Value],
                Kind = kind,
                Tags = tags ?? [],
                Tome = resolvedTome,
                ProjectId = projectId,
                SessionId = sessionId,
                Limit = limit,
            }, ct);

            return ToolResults.Success(new RecallView
            {
                Tome = resolvedTome,
                Matches = [.. matches.Select(ToHit)],
                Count = matches.Count,
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
}
