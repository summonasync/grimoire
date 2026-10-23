using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
        "with your actor provenance on it. Call it when the human states a preference, decision or correction: " +
        "write durable facts in English and keep the human's verbatim quotes as spoken; a near-duplicate " +
        "supersedes the older record, an identical write replays it, and never store secrets.";

    public Task<CallToolResult> Remember(
        [Description("The memory text. Make it self-contained - understandable months later without this conversation.")]
        string content,
        [Description("Session expires after ~24h; Project needs projectId; Global and Personal live across projects.")]
        [AllowedValues("Session", "Project", "Global", "Personal")]
        string scope,
        [Description("Journal for append-style observations; Fact for curated standing statements.")]
        [AllowedValues("Journal", "Fact")]
        string kind = "Journal",
        [Description("Required for scope=Project: the project key this memory belongs to. Omit for any other scope.")]
        string? projectId = null,
        [Description("Only for scope=Session: identifies the current session.")]
        string? sessionId = null,
        [Description("Optional free-form tags used by filtered recall and list; a board-entity link rides as a tag shaped overmind://entry/<uuid>.")]
        string[]? tags = null,
        [Description("0..1, default 0.5. Higher survives recency decay longer; use 0.9+ for hard decisions and rules.")]
        double importance = 0.5,
        [Description("Tome the memory belongs to. Writes are tome-bound: passing tome is how a write lands in another tome you own; omit it to write into your resolved tome. The response echoes where the memory landed.")]
        string? tome = null,
        CancellationToken cancellationToken = default)
    {
        var (scopeValue, scopeError) = EnumParams.ParseRequired<MemoryScope>(scope, nameof(scope));
        if (scopeError is not null)
        {
            return Task.FromResult(ToolResults.Failure(scopeError.NothingApplied()));
        }

        var (kindValue, kindError) = EnumParams.Parse<MemoryKind>(kind, nameof(kind));
        if (kindError is not null)
        {
            return Task.FromResult(ToolResults.Failure(kindError.NothingApplied()));
        }

        var kindEffective = kindValue ?? MemoryKind.Journal;

        if (string.IsNullOrWhiteSpace(content))
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                $"content: '{content}' must not be empty or whitespace.",
                nameof(content)).NothingApplied()));
        }

        if (!double.IsFinite(importance) || importance is < 0 or > 1)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                $"importance must be a finite number within 0..1 - got '{importance}'.",
                nameof(importance)).NothingApplied()));
        }

        if (scopeValue == MemoryScope.Project && string.IsNullOrWhiteSpace(projectId))
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.ProjectIdRequired,
                "scope 'Project' requires projectId (got scope 'Project', no projectId): a project memory without " +
                "one would be matched against every project and could supersede another project's record. Pass " +
                "projectId, or use scope 'Global' when the memory is not project-specific.",
                nameof(projectId)).NothingApplied()));
        }

        if (scopeValue != MemoryScope.Project && projectId is not null)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.ParamConflict,
                $"projectId only applies to scope 'Project'; omit it or change the scope " +
                $"(got projectId '{projectId}' with scope '{scope}').",
                nameof(projectId)).NothingApplied()));
        }

        if (scopeValue != MemoryScope.Session && sessionId is not null)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.ParamConflict,
                $"sessionId only applies to scope 'Session'; omit it or change the scope " +
                $"(got sessionId '{sessionId}' with scope '{scope}').",
                nameof(sessionId)).NothingApplied()));
        }

        if (scopeValue == MemoryScope.Session && kindValue == MemoryKind.Fact)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                "scope 'Session' only accepts kind 'Journal': session memories expire within a day and cannot hold " +
                $"standing facts (got kind '{kind}'). Store the durable statement in scope 'Project' (with projectId) " +
                "or scope 'Global'.",
                nameof(kind)).NothingApplied()));
        }

        if (TomeParams.Validate(tome, nameof(tome)) is { } tomeError)
        {
            return Task.FromResult(ToolResults.Failure(tomeError.NothingApplied()));
        }

        return ToolResults.ExecuteAsync(async ct =>
        {
            var resolvedTome = await actors.ResolveTomeAsync(tome, ct);
            var result = await memories.RememberAsync(new RememberRequest
            {
                Content = content,
                Scope = scopeValue,
                Kind = kindEffective,
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
