using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class BriefTool(BriefService brief, MemoryActorProvider actors)
{
    public const string Name = "memory_brief";

    public const string Title = "Get the startup brief";

    public const string Description =
        "Load the session-start payload in one call: persona, top-scored facts, recent journal, the tombstone " +
        "digest, and the startup contract block in hint - read it here once instead of chaining persona_get, " +
        "recall and list. Sections fill in priority order under a char budget, journal bodies are capped per " +
        "entry and flagged truncated, and what no longer fits is named in omitted; state empty is cold start, " +
        "not failure.";

    public Task<CallToolResult> Brief(
        [Description("Restrict to memories of one project.")]
        string? projectId = null,
        [Description("Restrict facts, journal and tombstones to these scopes; defaults to Project and Global, Session is never briefed.")]
        [AllowedValues("Session", "Project", "Global", "Personal")]
        string[]? scopes = null,
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
        var (scopesFilter, scopesError) = EnumParams.ParseEntries<MemoryScope>(scopes, nameof(scopes));
        if (scopesError is not null)
        {
            return Task.FromResult(ToolResults.Failure(scopesError));
        }

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
                Scopes = scopesFilter ?? [],
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
                Hint = IsEmpty(result) ? $"{StartupContract} {EmptyTomeHint}" : StartupContract,
                CompactionDue = ToCompactionSection(result.CompactionDue),
            });
        }, cancellationToken);
    }

    private static BriefCompactionSection? ToCompactionSection(BriefCompactionDue? due) =>
        due is null ? null : new BriefCompactionSection { PendingEntries = due.PendingEntries };

    private const string EmptyTomeHint = "tome is empty — use memory_scry for cross-tome context";

    private const string StartupContract =
        "startup contract - how to work this server. At session start call memory_persona_get before writing " +
        "in the human's voice or making style calls - check, don't guess, and never invent empty persona " +
        "sections; run memory_recall before redoing work that may already have a decision on record. Write " +
        "with memory_remember when the human states a preference, decision or correction - record it as they " +
        "said it, not as your summary; kind Fact for statements that must still read true next week, Journal " +
        "for progress notes; scope Session dies with the session, Project needs projectId, Global is " +
        "cross-project truth, Personal is identity-level; the persona itself is human-curated, never write it " +
        "here. A near-duplicate write supersedes the older record and names the replaced id; retrying an " +
        "identical write replays it; maintenancePending=true means the record committed but duplicate cleanup " +
        "did not finish - replaying converges. Before you overwrite or retract a hit, walk memory_history so " +
        "the correction lands on the right generation; memory_forget keeps your reason on the tombstone for " +
        "the human to audit (inspect it via memory_list status=Tombstoned - nothing is hard-deleted, GC owns " +
        "that); memory_restore reverses a correction that turned out wrong. Compaction loop: when memory_stats " +
        "reports pendingCompaction or this reply carries compactionDue, pull memory_compact_candidates, write " +
        "one kind=Fact memory_remember per " +
        "cluster, then memory_forget each consumed entry with a reason like 'compacted into <new id>'; never " +
        "tombstone entries you did not actually distill. Conventions: reads accept tome= for another tome you " +
        "own, writes never cross tomes, memory_scry is the one cross-tome read; upstream_unavailable means the " +
        "store or embedding provider is unreachable - reads returned nothing, writes landed nothing, and the " +
        "retry is safe when infra is back.";

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
