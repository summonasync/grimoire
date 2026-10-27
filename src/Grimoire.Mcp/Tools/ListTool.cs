using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class ListTool(MemoryService memories, MemoryActorProvider actors)
{
    public const string Name = "memory_list";

    public const string Title = "List memories";

    public const string Description =
        "Browse stored memories by filter, newest first - full records, Active by default, with status, actor and " +
        "tags; pass status to audit Superseded or Tombstoned rows, for example to confirm a Fact was already " +
        "retracted. Pass fields or compact to project each entry down to the keys you need - the cheap path for " +
        "periodic scans. Prefer memory_recall for meaning-based lookup; reads are tome-scoped and the response " +
        "echoes the tome listed.";

    private static readonly ReadProjectionSurface Surface = new()
    {
        AllowedKeys =
        [
            "content", "scope", "kind", "status", "actor", "tags", "projectId", "sessionId",
            "importance", "accessCount", "supersededBy", "tombstoneReason", "expiresAt",
            "createdAt", "lastAccessedAt", "bodyChars",
        ],
        AlwaysOnKeys = ["memoryId"],
        CompactKeys = ["memoryId", "content", "tags", "createdAt"],
    };

    public Task<CallToolResult> List(
        [Description("Restrict to one scope.")]
        [AllowedValues("Session", "Project", "Global", "Personal")]
        string? scope = null,
        [Description("Restrict to Journal or Fact records.")]
        [AllowedValues("Journal", "Fact")]
        string? kind = null,
        [Description("Active by default; Superseded shows dedup history; Tombstoned shows retracted entries.")]
        [AllowedValues("Active", "Superseded", "Tombstoned")]
        string status = "Active",
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
        [Description("Project each entry to these camelCase keys, e.g. [\"content\", \"createdAt\"]; memoryId is always included and an unknown field is rejected naming the allowed set.")]
        string[]? fields = null,
        [Description("Project each entry to the digest shape (memoryId, content, tags, createdAt) - the cheap path for periodic scans; mutually exclusive with fields.")]
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

        var (statusFilter, statusError) = EnumParams.Parse<MemoryStatus>(status, nameof(status));
        if (statusError is not null)
        {
            return Task.FromResult(ToolResults.Failure(statusError));
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
            var records = await memories.ListAsync(new MemoryFilter
            {
                Scopes = scopeFilter is null ? [] : [scopeFilter.Value],
                Kind = kindFilter,
                Status = statusFilter ?? MemoryStatus.Active,
                Tags = tags ?? [],
                Tome = resolvedTome,
                ProjectId = projectId,
                SessionId = sessionId,
                Limit = limit,
            }, ct);

            var entries = records.Select(ToEntry).ToArray();
            if (projectedKeys is null)
            {
                return ToolResults.Success(new ListView
                {
                    Tome = resolvedTome,
                    Memories = entries,
                    Count = entries.Length,
                });
            }

            return ToolResults.Success(new ProjectedListView
            {
                Tome = resolvedTome,
                Memories = [.. entries.Select(entry => ProjectEntry(entry, projectedKeys))],
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

    private static IReadOnlyDictionary<string, object?> ProjectEntry(MemoryListEntry entry, IReadOnlyList<string> keys)
    {
        var projected = new Dictionary<string, object?>(keys.Count, StringComparer.Ordinal);
        foreach (var key in keys)
        {
            projected[key] = key switch
            {
                "memoryId" => entry.MemoryId,
                "content" => entry.Content,
                "scope" => entry.Scope,
                "kind" => entry.Kind,
                "status" => entry.Status,
                "actor" => entry.Actor,
                "tags" => entry.Tags,
                "projectId" => entry.ProjectId,
                "sessionId" => entry.SessionId,
                "importance" => entry.Importance,
                "accessCount" => entry.AccessCount,
                "supersededBy" => entry.SupersededBy,
                "tombstoneReason" => entry.TombstoneReason,
                "expiresAt" => entry.ExpiresAt,
                "createdAt" => entry.CreatedAt,
                "lastAccessedAt" => entry.LastAccessedAt,
                "bodyChars" => entry.BodyChars,
                _ => throw new UnreachableException($"key '{key}' passed validation but has no projection."),
            };
        }

        return projected;
    }
}
