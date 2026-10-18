using System.ComponentModel;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class ForgetTool(MemoryService memories, MemoryActorProvider actors)
{
    public const string Name = "memory_forget";

    public const string Title = "Forget a memory";

    public const string Description =
        "Tombstone a memory that is wrong, outdated or retracted, and get back whether it landed: memoryId, tome, " +
        "forgotten and your reason on success. Nothing is hard-deleted: the record stays stored with a visible " +
        "tombstone and your reason, but disappears from recall and from list's default (active) view. " +
        "Call it when the human retracts a fact or reverses a decision (\"forget that, we do it differently now\"), " +
        "and to retire journal entries after distilling them into facts. Use the exact memoryId from an earlier " +
        "recall, list or compact-candidates result. A tombstone lands only in the resolved tome: an id that lives " +
        "in another tome comes back forgotten=false - reads accept tome=, writes never cross. Forgetting an " +
        "unknown or already-forgotten id also returns forgotten=false - do not retry. upstream_unavailable means " +
        "the store is unreachable and no tombstone was applied.";

    public Task<CallToolResult> Forget(
        [Description("The id of the memory to tombstone, exactly as returned by a previous tool call.")]
        string memoryId,
        [Description("Why the memory is being retracted. Recorded on the tombstone for the human to audit.")]
        string reason,
        [Description("Tome the tombstone lands in; omit for your resolved tome. Forgetting never crosses tomes: an id from another tome returns forgotten=false.")]
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

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                "reason must not be empty: every tombstone must say why it exists.",
                nameof(reason))));
        }

        if (TomeParams.Validate(tome, nameof(tome)) is { } tomeError)
        {
            return Task.FromResult(ToolResults.Failure(tomeError));
        }

        return ToolResults.ExecuteAsync(async ct =>
        {
            var resolvedTome = await actors.ResolveTomeAsync(tome, ct);
            var forgotten = await memories.ForgetAsync(id, reason, ct, resolvedTome);

            return ToolResults.Success(new ForgetView
            {
                MemoryId = id,
                Tome = resolvedTome,
                Forgotten = forgotten,
                Reason = forgotten ? reason : null,
            });
        }, cancellationToken);
    }
}
