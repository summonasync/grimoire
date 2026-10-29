using System.ComponentModel;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class RestoreTool(CorrectionService corrections, IMemoryStore store, MemoryActorProvider actors)
{
    public const string Name = "memory_restore";

    public const string Title = "Restore a memory";

    public const string Description =
        "The undo door for a memory whose earlier correction turned out wrong: the record returns to active and " +
        "the correction that buried it is tombstoned on the correction trail, leaving one active truth per fact. " +
        "Unknown or foreign-tome ids come back restored=false; repeating a restore is safe - it converges a " +
        "half-applied reversal and is otherwise a no-op.";

    public Task<CallToolResult> Restore(
        [Description("The id of the memory to bring back, exactly as returned by a previous tool call.")]
        string memoryId,
        [Description("Why the earlier correction is being reversed. Recorded on the correction trail for the human to audit.")]
        string reason,
        [Description("Tome the restore happens in; omit for your resolved tome. Restoring never crosses tomes: an id from another tome returns restored=false.")]
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
                $"reason: '{reason}' is blank: every restore must say why the earlier correction is reversed.",
                nameof(reason)).NothingApplied()));
        }

        if (TomeParams.Validate(tome, nameof(tome)) is { } tomeError)
        {
            return Task.FromResult(ToolResults.Failure(tomeError.NothingApplied()));
        }

        return ToolResults.ExecuteAsync(async ct =>
        {
            var resolvedTome = await actors.ResolveTomeAsync(tome, ct);
            var result = await corrections.RestoreAsync(id, reason, ct, resolvedTome, await actors.GetActorAsync(ct));

            return ToolResults.Success(new RestoreView
            {
                MemoryId = id,
                Tome = resolvedTome,
                Restored = result.Restored,
                Reason = result.Restored ? reason : null,
                Superseder = await DescribeSupersederAsync(result, ct),
            });
        }, cancellationToken);
    }

    private async Task<SupersederView?> DescribeSupersederAsync(RestoreResult result, CancellationToken cancellationToken)
    {
        if (result.Record?.SupersededBy is not { } successorId)
        {
            return null;
        }

        var successor = await store.GetAsync(successorId, cancellationToken);
        return successor is null
            ? null
            : new SupersederView
            {
                MemoryId = successor.Id,
                Tombstoned = successor.Status == MemoryStatus.Tombstoned,
                TombstoneReason = successor.TombstoneReason,
            };
    }
}
