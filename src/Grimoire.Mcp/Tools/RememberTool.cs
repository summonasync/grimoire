using System.ComponentModel;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class RememberTool(MemoryService memories, MemoryActorProvider actors)
{
    public const string Name = "memory_remember";

    public const string Title = "Remember a memory";

    public const string Description =
        "Write a durable memory and get back the stored record - id, tome, scope, kind and supersede outcome - " +
        "with your actor provenance on it. Call it when the human states a preference, decision or correction " +
        "and record it verbatim; a near-duplicate supersedes the older record, an identical write replays it, " +
        "and never store secrets.";

    public Task<CallToolResult> Remember(
        [Description("The memory text. Make it self-contained - understandable months later without this conversation.")]
        string content,
        [Description("session expires after ~24h; project needs projectId; global and personal live across projects.")]
        MemoryScope scope,
        [Description("journal for append-style observations; fact for curated standing statements.")]
        MemoryKind kind = MemoryKind.Journal,
        [Description("Required for scope=project: the project key this memory belongs to. Omit for any other scope.")]
        string? projectId = null,
        [Description("Only for scope=session: identifies the current session.")]
        string? sessionId = null,
        [Description("Optional free-form tags used by filtered recall and list.")]
        string[]? tags = null,
        [Description("0..1, default 0.5. Higher survives recency decay longer; use 0.9+ for hard decisions and rules.")]
        double importance = 0.5,
        [Description("Tome the memory belongs to. Writes are tome-bound: passing tome is how a write lands in another tome you own; omit it to write into your resolved tome. The response echoes where the memory landed.")]
        string? tome = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                "content must not be empty or whitespace.",
                nameof(content))));
        }

        if (!double.IsFinite(importance) || importance is < 0 or > 1)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                "importance must be a finite number within 0..1.",
                nameof(importance))));
        }

        if (scope == MemoryScope.Project && string.IsNullOrWhiteSpace(projectId))
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.ProjectIdRequired,
                "scope 'project' requires projectId: a project memory without one would be matched against every " +
                "project and could supersede another project's record. Pass projectId, or use scope 'global' when " +
                "the memory is not project-specific.",
                nameof(projectId))));
        }

        if (scope != MemoryScope.Project && projectId is not null)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.ParamConflict,
                $"projectId only applies to scope 'project'; omit it or change the scope (got scope '{scope}').",
                nameof(projectId))));
        }

        if (scope != MemoryScope.Session && sessionId is not null)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.ParamConflict,
                $"sessionId only applies to scope 'session'; omit it or change the scope (got scope '{scope}').",
                nameof(sessionId))));
        }

        if (scope == MemoryScope.Session && kind == MemoryKind.Fact)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                "scope 'session' only accepts kind 'journal': session memories expire within a day and cannot hold " +
                "standing facts. Store the durable statement in scope 'project' or 'global'.",
                nameof(kind))));
        }

        if (TomeParams.Validate(tome, nameof(tome)) is { } tomeError)
        {
            return Task.FromResult(ToolResults.Failure(tomeError));
        }

        return ToolResults.ExecuteAsync(async ct =>
        {
            var resolvedTome = await actors.ResolveTomeAsync(tome, ct);
            var result = await memories.RememberAsync(new RememberRequest
            {
                Content = content,
                Scope = scope,
                Kind = kind,
                Actor = actors.GetActor(),
                Tome = resolvedTome,
                ProjectId = projectId,
                SessionId = sessionId,
                Tags = tags ?? [],
                Importance = importance,
            }, ct);

            return ToolResults.Success(new RememberView
            {
                MemoryId = result.Record.Id,
                Scope = result.Record.Scope,
                Kind = result.Record.Kind,
                Actor = result.Record.Actor.ToString(),
                Tome = result.Record.Tome,
                ProjectId = result.Record.ProjectId,
                SessionId = result.Record.SessionId,
                Tags = result.Record.Tags,
                SupersededExisting = result.SupersededExisting,
                SupersededMemoryId = result.SupersededId,
                IdempotentReplay = result.IdempotentReplay,
                MaintenancePending = result.MaintenancePending,
                BodyChars = content.Length,
                CreatedAt = result.Record.CreatedAt,
            });
        }, cancellationToken);
    }
}
