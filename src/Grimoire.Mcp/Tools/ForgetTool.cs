using System.ComponentModel;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class ForgetTool(MemoryService memories)
{
    public const string Name = "memory_forget";

    public const string Title = "Forget a memory";

    public const string Description =
        "Tombstone a memory that is wrong, outdated or retracted. Nothing is hard-deleted: the record stays stored " +
        "with a visible tombstone and your reason, but disappears from recall and from list's default (active) view. " +
        "Call it when the human retracts a fact or reverses a decision (\"forget that, we do it differently now\"), " +
        "and to retire journal entries after distilling them into facts. Use the exact memoryId from an earlier " +
        "recall, list or compact-candidates result. Forgetting an unknown or already-forgotten id returns " +
        "forgotten=false - do not retry.";

    public Task<CallToolResult> Forget(
        [Description("The id of the memory to tombstone, exactly as returned by a previous tool call.")]
        string memoryId,
        [Description("Why the memory is being retracted. Recorded on the tombstone for the human to audit.")]
        string reason,
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

        return ToolResults.ExecuteAsync(async ct =>
        {
            var forgotten = await memories.ForgetAsync(id, reason, ct);

            return ToolResults.Success(new ForgetView
            {
                MemoryId = id,
                Forgotten = forgotten,
                Reason = forgotten ? reason : null,
            });
        }, cancellationToken);
    }
}
