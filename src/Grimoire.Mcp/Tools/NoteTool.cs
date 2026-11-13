using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class NoteTool(
    MemoryService memories,
    MemoryActorProvider actors,
    ProjectTagResolver projectTags,
    ICallerPathProvider callerPaths)
{
    public const string Name = "memory_note";

    public const string Title = "Write a quick note";

    public const string Description =
        "Zero-ceremony journal write: supply content, the server does the bookkeeping - kind is always " +
        "Journal, importance is fixed at 0.5. Pass topic to force a topic:<value> stamp; without it the top " +
        "embedding neighbors vote and two agreeing neighbors at similarity >= 0.75 stamp their shared topic, " +
        "a winning vote under the confidence threshold stamps nothing and topicRationale says why. Scope " +
        "defaults to 'Project' and resolves projectId from the caller's working directory via the project " +
        "registry (an unresolved cwd is rejected with the registered aliases); 'Session' keeps a ~24h TTL; " +
        "'Global' and 'Personal' live across projects. The response is one payload carrying memoryId, scope, " +
        "projectId, topicTag, topicDecision, topicRationale and nearDuplicates; an identical retry replays " +
        "idempotently. For durable curated facts use memory_remember instead.";

    public Task<CallToolResult> Note(
        [Description("The note text. Make it self-contained - understandable months later without this conversation.")]
        string content,
        [Description("Defaults to 'Project'. Session expires after ~24h; Project needs a registered projectId (or auto-tag from the working directory); Global and Personal live across projects.")]
        [AllowedValues("Session", "Project", "Global", "Personal")]
        string scope = "Project",
        [Description("Registered project alias. Required for scope=Project unless the working directory matches a registered project path (auto-tag); unknown values are rejected with the registered set. Optional provenance on Global/Personal.")]
        string? projectId = null,
        [Description("Only for scope=Session: identifies the current session.")]
        string? sessionId = null,
        [Description("Explicit topic hint, normalized to topic:<value> and stamped over the neighbor vote; omit it to let the neighbors classify.")]
        string? topic = null,
        [Description("Optional free-form tags merged with the server-stamped topic tag.")]
        string[]? tags = null,
        [Description("Tome the note belongs to. Writes are tome-bound: passing tome is how a note lands in another tome you own; omit it to write into your resolved tome. The response echoes where the note landed.")]
        string? tome = null,
        CancellationToken cancellationToken = default)
    {
        var (scopeValue, scopeError) = EnumParams.Parse<MemoryScope>(scope, nameof(scope));
        if (scopeError is not null)
        {
            return Task.FromResult(ToolResults.Failure(scopeError.NothingApplied()));
        }

        var scopeEffective = scopeValue ?? MemoryScope.Project;

        if (string.IsNullOrWhiteSpace(content))
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                $"content: '{content}' must not be empty or whitespace.",
                nameof(content)).NothingApplied()));
        }

        if (scopeEffective == MemoryScope.Session && projectId is not null)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.ParamConflict,
                "projectId only applies to scopes 'Project', 'Global' and 'Personal'; omit it or change the scope " +
                $"(got projectId '{projectId}' with scope '{scopeEffective}').",
                nameof(projectId)).NothingApplied()));
        }

        var projectTag = projectTags.Resolve(scopeEffective, projectId, callerPaths.Path);
        if (projectTag.Rejection is { } rejection)
        {
            return Task.FromResult(ToolResults.Failure(ProjectTagError(rejection, projectTag, projectId).NothingApplied()));
        }

        if (scopeEffective != MemoryScope.Session && sessionId is not null)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.ParamConflict,
                $"sessionId only applies to scope 'Session'; omit it or change the scope " +
                $"(got sessionId '{sessionId}' with scope '{scopeEffective}').",
                nameof(sessionId)).NothingApplied()));
        }

        if (TomeParams.Validate(tome, nameof(tome)) is { } tomeError)
        {
            return Task.FromResult(ToolResults.Failure(tomeError.NothingApplied()));
        }

        return ToolResults.ExecuteAsync(async ct =>
        {
            var resolvedTome = await actors.ResolveTomeAsync(tome, ct);
            var note = await memories.NoteAsync(new NoteRequest
            {
                Content = content,
                Scope = scopeEffective,
                Actor = await actors.GetActorAsync(ct),
                Tome = resolvedTome,
                ProjectId = projectTag.ProjectId,
                SessionId = sessionId,
                Topic = topic,
                Tags = tags ?? [],
            }, ct);

            var result = note.Result;

            return ToolResults.Success(new NoteView
            {
                MemoryId = result.Record.Id,
                Scope = result.Record.Scope,
                Actor = result.Record.Actor.ToString(),
                Tome = result.Record.Tome,
                ProjectId = result.Record.ProjectId,
                Tags = result.Record.Tags,
                TopicTag = note.Vote?.Tag ?? TopicTagOf(result.Record.Tags),
                TopicDecision = note.Vote?.Decision.ToString("G"),
                TopicRationale = RationaleOf(note.Vote),
                SupersededExisting = result.SupersededExisting,
                SupersededMemoryId = result.SupersededId,
                NearDuplicates = result.NearMatches.Count == 0
                    ? null
                    : [.. result.NearMatches.Select(match => new NearDuplicateView
                      {
                          MemoryId = match.MemoryId,
                          Similarity = match.Similarity,
                          Snippet = match.Snippet,
                      })],
                IdempotentReplay = result.IdempotentReplay,
                MaintenancePending = result.MaintenancePending,
                CreatedAt = result.Record.CreatedAt,
            });
        }, cancellationToken);
    }

    private static string? TopicTagOf(IReadOnlyList<string> tags) => tags.FirstOrDefault(tag =>
        tag.StartsWith(ITopicClassifier.TopicPrefix, StringComparison.Ordinal));

    private static string? RationaleOf(TopicVote? vote) => vote switch
    {
        null => null,
        { Decision: TopicVoteDecision.Explicit } => "explicit topic hint; neighbors were not consulted",
        { Decision: TopicVoteDecision.Voted } =>
            $"{vote.WinningCount} neighbors: {vote.Tag} (best similarity {vote.BestSimilarity:F2})",
        { Decision: TopicVoteDecision.BelowConfidence } =>
            $"{vote.WinningCount} neighbors agreed on {vote.CandidateTag} at best similarity {vote.BestSimilarity:F2}, " +
            "under the topic confidence threshold, so nothing was stamped; pass topic to stamp it anyway",
        _ => NoConsensusRationale(vote),
    };

    private static string NoConsensusRationale(TopicVote vote)
    {
        var counted = vote.Neighbors
            .Where(entry => entry.Counted)
            .Select(entry => entry.Tag)
            .Distinct()
            .ToArray();
        var bare = vote.Neighbors
            .Where(entry => !entry.Counted)
            .Select(entry => entry.Tag)
            .Distinct()
            .Take(4)
            .ToArray();

        if (counted.Length == 0 && bare.Length == 0)
        {
            return "no qualifying neighbor carried a tag";
        }

        var seen = counted.Length == 0
            ? "no neighbor carried a topic:<value> tag"
            : $"topic tags seen, none with enough agreement: {string.Join(", ", counted)}";

        return bare.Length == 0 ? seen : $"{seen}; bare tags do not vote: {string.Join(", ", bare)}";
    }

    private ToolError ProjectTagError(ProjectTagRejection rejection, ProjectTagResolution resolution, string? projectId)
    {
        var allowed = string.Join(", ", resolution.AllowedAliases);
        return rejection switch
        {
            ProjectTagRejection.Unknown => new ToolError(
                ToolErrorCodes.ProjectIdUnknown,
                resolution.AllowedAliases.Count == 0
                    ? $"projectId '{projectId}' is not a registered project and the project registry is empty - " +
                      "register the project in the Grimoire config first."
                    : $"projectId: '{projectId}' is not a registered project alias. Allowed projectId values " +
                      $"(case-sensitive, pass one exactly as advertised): {allowed}.",
                nameof(projectId)),
            _ => new ToolError(
                ToolErrorCodes.ProjectIdRequired,
                "scope 'Project' requires projectId (got scope 'Project', no projectId): the working directory " +
                $"'{callerPaths.Path}' matches no registered project path. " +
                (resolution.AllowedAliases.Count == 0
                    ? "The project registry is empty; register the project in the Grimoire config first. "
                    : $"Pass one of the registered projectId values (case-sensitive, pass one exactly as advertised): {allowed}. ") +
                "Or use scope 'Global' when the memory is not project-specific.",
                nameof(projectId)),
        };
    }
}
