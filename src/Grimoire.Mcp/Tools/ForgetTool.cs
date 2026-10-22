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
        "Tombstone a memory that is wrong or retracted - nothing hard-deletes: the record stays stored with your " +
        "reason and vanishes from recall and the default active list. Pass an exact memoryId from an earlier result; " +
        "an unknown, already-forgotten or foreign-tome id comes back forgotten=false - do not retry.";

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
                $"memoryId: '{memoryId}' is not a canonical UUID - pass the id exactly as returned by a previous memory tool call.",
                nameof(memoryId)).NothingApplied()));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                $"reason: '{reason}' is blank: every tombstone must say why it exists.",
                nameof(reason)).NothingApplied()));
        }

        if (TomeParams.Validate(tome, nameof(tome)) is { } tomeError)
        {
            return Task.FromResult(ToolResults.Failure(tomeError.NothingApplied()));
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
