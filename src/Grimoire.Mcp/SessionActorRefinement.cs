using Grimoire.Core;

namespace Grimoire.Mcp;

internal sealed class SessionActorRefinement(
    ISessionRegistry registry,
    ISessionCaptureFaultSink faults,
    TimeSpan writeBudget)
{
    private readonly object _gate = new();

    private Guid? rowId;
    private MemoryActor? pendingIdentity;
    private bool claimed;

    public void RowWritten(Guid id)
    {
        lock (_gate)
        {
            rowId = id;
        }

        if (TryClaim(out var point, out var identity))
        {
            Flush(point, identity);
        }
    }

    public void IdentitySet(MemoryActor identity)
    {
        lock (_gate)
        {
            pendingIdentity ??= identity;
        }

        if (TryClaim(out var point, out var first))
        {
            Flush(point, first);
        }
    }

    private bool TryClaim(out Guid point, out MemoryActor identity)
    {
        lock (_gate)
        {
            if (claimed || rowId is not { } id || pendingIdentity is not { } actor)
            {
                point = default;
                identity = default;
                return false;
            }

            claimed = true;
            rowId = null;
            pendingIdentity = null;
            point = id;
            identity = actor;
            return true;
        }
    }

    private void Flush(Guid point, MemoryActor identity) =>
        _ = Task.Run(() => RefineAsync(point, identity));

    private async Task RefineAsync(Guid point, MemoryActor identity)
    {
        using var budget = new CancellationTokenSource(writeBudget);
        try
        {
            await registry.RefineActorAsync(
                point,
                identity.Alias,
                identity.Vendor,
                budget.Token);
        }
        catch (Exception failure)
        {
            faults.CaptureFailed(Describe(failure));
        }
    }

    private string Describe(Exception failure) =>
        failure is OperationCanceledException
            ? $"session actor refinement abandoned after the {writeBudget} write budget"
            : $"session actor refinement failed: {failure.Message}";
}
