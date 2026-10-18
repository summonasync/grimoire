using System.ComponentModel;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class HistoryTool(CorrectionService corrections, MemoryActorProvider actors)
{
    public const string Name = "memory_history";

    public const string Title = "Show a memory's replacement history";

    public const string Description =
        "Walk a memory's full replacement lineage before trusting it or writing over it. Use it when a recall or " +
        "list hit looks stale, when you want to know what a fact replaced or what replaced it, and before a forget " +
        "or restore so the correction lands on the right generation of the fact. Returns the whole supersede chain " +
        "- predecessors and successors - ordered oldest to newest, each entry with status, tome, creation and " +
        "tombstone timestamps, tombstone reason, actor and a 120-character body preview. Read-only and " +
        "tome-scoped: an id that does not exist in the resolved tome returns found=false with an empty chain, not " +
        "an error - pass tome to read the lineage in another tome you own. upstream_unavailable means the store " +
        "is unreachable; the call writes nothing.";

    public Task<CallToolResult> History(
        [Description("The id of the memory whose lineage you want, exactly as returned by a previous tool call.")]
        string memoryId,
        [Description("Tome to read the lineage from; omit for your resolved tome. History never crosses tomes: an id from another tome returns found=false.")]
        string? tome = null,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParseExact(memoryId, "D", out var id))
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                "memoryId must be a canonical UUID, exactly as returned by a previous memory tool call.",
                nameof(memoryId))));
        }

        if (TomeParams.Validate(tome, nameof(tome)) is { } tomeError)
        {
            return Task.FromResult(ToolResults.Failure(tomeError));
        }

        return ToolResults.ExecuteAsync(async ct =>
        {
            var resolvedTome = await actors.ResolveTomeAsync(tome, ct);
            var result = await corrections.HistoryAsync(id, ct, resolvedTome);

            return ToolResults.Success(new HistoryView
            {
                MemoryId = id,
                Tome = resolvedTome,
                Found = result.Found,
                Chain = [.. result.Chain.Select(ToEntry)],
            });
        }, cancellationToken);
    }

    private static HistoryEntryView ToEntry(HistoryEntry entry)
    {
        return new HistoryEntryView
        {
            MemoryId = entry.Id,
            Status = entry.Status,
            Tome = entry.Tome,
            CreatedAt = entry.CreatedAt,
            TombstonedAt = entry.TombstonedAt,
            TombstoneReason = entry.TombstoneReason,
            Actor = entry.Actor.ToString(),
            BodyPreview = entry.BodyPreview,
        };
    }
}
