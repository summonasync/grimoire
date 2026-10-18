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
        "The undo door of the memory system: bring back a memory whose earlier correction turned out wrong. Use it " +
        "when you tombstoned a fact that is true again, or a newer memory superseded an older one that was right " +
        "all along and the old truth must return. A restore is total: the record goes back to active with its " +
        "tombstone lifted, and the superseding memory is tombstoned with reason \"restored <id>\" so exactly one " +
        "active truth per fact remains. Repeating a restore is a safe no-op. An id that does not exist or lives in " +
        "another tome comes back restored=false - an honest answer, not an error; writes never cross tomes. The " +
        "response echoes the resolved tome and, when the record had a successor, where that superseder stands now. " +
        "Every restore is recorded on the memory's correction trail with your reason, for the human to audit.";

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
                "memoryId must be a canonical UUID, exactly as returned by a previous memory tool call.",
                nameof(memoryId))));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidRequest,
                "reason must not be empty: every restore must say why the earlier correction is reversed.",
                nameof(reason))));
        }

        if (TomeParams.Validate(tome, nameof(tome)) is { } tomeError)
        {
            return Task.FromResult(ToolResults.Failure(tomeError));
        }

        return ToolResults.ExecuteAsync(async ct =>
        {
            var resolvedTome = await actors.ResolveTomeAsync(tome, ct);
            var result = await corrections.RestoreAsync(id, reason, ct, resolvedTome, actors.GetActor());

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
