using System.ComponentModel;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class BriefTool(BriefService brief, MemoryActorProvider actors)
{
    public const string Name = "memory_brief";

    public const string Title = "Get the startup brief";

    public const string Description =
        "Load the agent startup brief in one call - persona, top-scored facts, recent journal and the tombstone " +
        "digest - instead of chaining memory_persona_get, memory_recall and memory_list at session start. Sections " +
        "fill in priority order (persona, facts, tombstones, journal) under a total char budget; chars are the size " +
        "proxy, not exact tokens, and a section that no longer fits is named in omitted - truncation is never " +
        "silent. Journal bodies are capped per entry and flagged truncated; tombstones are references only, inspect " +
        "one via memory_list status=Tombstoned. A section in state empty means cold start: nothing recorded there " +
        "yet, not a failure - when every section comes back empty the response says so and points at memory_scry " +
        "for cross-tome context. The brief is scoped to one tome; pass tome to brief another tome you own. " +
        "upstream_unavailable means the store or embedding provider is unreachable; the call is read-only and " +
        "writes nothing.";

    public Task<CallToolResult> Brief(
        [Description("Restrict to memories of one project.")]
        string? projectId = null,
        [Description("Restrict facts, journal and tombstones to these scopes; defaults to project and global, session is never briefed.")]
        MemoryScope[]? scopes = null,
        [Description("Top facts per scope, default 5, clamped server-side.")]
        int? factsPerScope = null,
        [Description("Recent journal entries per scope, default 5, clamped server-side.")]
        int? journalPerScope = null,
        [Description("Tombstoned records in the digest, default 10, clamped server-side.")]
        int? tombstoneLimit = null,
        [Description("Tome to brief; omit for your resolved tome.")]
        string? tome = null,
        CancellationToken cancellationToken = default)
    {
        if (TomeParams.Validate(tome, nameof(tome)) is { } tomeError)
        {
            return Task.FromResult(ToolResults.Failure(tomeError));
        }

        return ToolResults.ExecuteAsync(async ct =>
        {
            var resolvedTome = await actors.ResolveTomeAsync(tome, ct);
            var result = await brief.BriefAsync(new BriefQuery
            {
                Tome = resolvedTome,
                ProjectId = projectId,
                Scopes = scopes ?? [],
                FactsPerScope = factsPerScope,
                JournalPerScope = journalPerScope,
                TombstoneLimit = tombstoneLimit,
            }, ct);

            return ToolResults.Success(new BriefView
            {
                Tome = resolvedTome,
                Persona = ToPersonaSection(result, resolvedTome),
                Facts = ToItemsSection(result.FactsState, result.Facts, ToFactItem),
                Tombstones = ToItemsSection(result.TombstonesState, result.Tombstones, ToTombstoneItem),
                Journal = ToItemsSection(result.JournalState, result.Journal, ToJournalItem),
                Omitted = [.. result.Omitted.Select(ToSectionName)],
                Hint = IsEmpty(result) ? EmptyTomeHint : null,
            });
        }, cancellationToken);
    }

    private const string EmptyTomeHint = "tome is empty — use memory_scry for cross-tome context";

    private static bool IsEmpty(BriefResult result) =>
        result.PersonaState == BriefSectionState.Empty &&
        result.FactsState == BriefSectionState.Empty &&
        result.TombstonesState == BriefSectionState.Empty &&
        result.JournalState == BriefSectionState.Empty;

    private static BriefPersonaSection ToPersonaSection(BriefResult result, string tome)
    {
        return new BriefPersonaSection
        {
            State = result.PersonaState,
            Document = result.Persona is null
                ? null
                : new PersonaView
                {
                    Tome = tome,
                    Identity = result.Persona.Identity,
                    Tone = result.Persona.Tone,
                    Preferences = result.Persona.Preferences,
                    Rules = result.Persona.Rules,
                },
        };
    }

    private static BriefItemsSection<TOut> ToItemsSection<TIn, TOut>(
        BriefSectionState state,
        IReadOnlyList<TIn> items,
        Func<TIn, TOut> map)
        where TIn : class
    {
        return new BriefItemsSection<TOut>
        {
            State = state,
            Items = [.. items.Select(map)],
        };
    }

    private static BriefFactItem ToFactItem(BriefFact fact)
    {
        return new BriefFactItem
        {
            MemoryId = fact.Id,
            Content = fact.Content,
            Scope = fact.Scope,
            Score = fact.Score,
            CreatedAt = fact.CreatedAt,
        };
    }

    private static BriefTombstoneItem ToTombstoneItem(BriefTombstone tombstone)
    {
        return new BriefTombstoneItem
        {
            MemoryId = tombstone.Id,
            Reason = tombstone.Reason,
            TombstonedAt = tombstone.TombstonedAt,
        };
    }

    private static BriefJournalItem ToJournalItem(BriefJournalEntry entry)
    {
        return new BriefJournalItem
        {
            MemoryId = entry.Id,
            Content = entry.Content,
            Truncated = entry.Truncated,
            Scope = entry.Scope,
            CreatedAt = entry.CreatedAt,
        };
    }

    private static string ToSectionName(BriefSection section) => section switch
    {
        BriefSection.Persona => "persona",
        BriefSection.Facts => "facts",
        BriefSection.Tombstones => "tombstones",
        BriefSection.Journal => "journal",
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown brief section."),
    };
}
