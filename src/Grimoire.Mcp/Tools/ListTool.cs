using System.ComponentModel;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class ListTool(MemoryService memories, MemoryActorProvider actors)
{
    public const string Name = "memory_list";

    public const string Title = "List memories";

    public const string Description =
        "Browse stored memories by filter, newest first - no semantic ranking. Defaults to active records; pass " +
        "status to audit superseded or tombstoned entries, for example to confirm a fact was already retracted " +
        "before re-asserting it. Pass tome to audit another tome you own. Prefer memory_recall for meaning-based " +
        "lookup.";

    public Task<CallToolResult> List(
        [Description("Restrict to one scope.")]
        MemoryScope? scope = null,
        [Description("Restrict to journal or fact records.")]
        MemoryKind? kind = null,
        [Description("active by default; superseded shows dedup history; tombstoned shows retracted entries.")]
        MemoryStatus status = MemoryStatus.Active,
        [Description("Restrict to memories of one project.")]
        string? projectId = null,
        [Description("Restrict to one session's memories.")]
        string? sessionId = null,
        [Description("Restrict to memories carrying any of these tags.")]
        string[]? tags = null,
        [Description("Maximum entries to return, default 50.")]
        int limit = 50,
        [Description("Tome to browse; omit for your resolved tome, or name another tome you own to audit it. The response echoes the tome listed.")]
        string? tome = null,
        CancellationToken cancellationToken = default)
    {
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
            var records = await memories.ListAsync(new MemoryFilter
            {
                Scopes = scope is null ? [] : [scope.Value],
                Kind = kind,
                Status = status,
                Tags = tags ?? [],
                Tome = resolvedTome,
                ProjectId = projectId,
                SessionId = sessionId,
                Limit = limit,
            }, ct);

            var entries = records.Select(ToEntry).ToArray();
            return ToolResults.Success(new ListView
            {
                Tome = resolvedTome,
                Memories = entries,
                Count = entries.Length,
            });
        }, cancellationToken);
    }

    private static MemoryListEntry ToEntry(MemoryRecord record)
    {
        return new MemoryListEntry
        {
            MemoryId = record.Id,
            Content = record.Content,
            Scope = record.Scope,
            Kind = record.Kind,
            Status = record.Status,
            Actor = record.Actor.ToString(),
            Tags = record.Tags,
            ProjectId = record.ProjectId,
            SessionId = record.SessionId,
            Importance = record.Importance,
            AccessCount = record.AccessCount,
            SupersededBy = record.SupersededBy,
            TombstoneReason = record.TombstoneReason,
            ExpiresAt = record.ExpiresAt,
            CreatedAt = record.CreatedAt,
            LastAccessedAt = record.LastAccessedAt,
            BodyChars = record.Content.Length,
        };
    }
}
