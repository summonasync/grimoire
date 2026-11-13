using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

public sealed class SessionActorRefinementTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task RowWrittenBeforeIdentitySet_RefinesOnTheFirstIdentityPair()
    {
        var sessions = new FakeSessionRegistry();
        var faults = new RecordingSessionCaptureFaultSink();
        var refinement = new SessionActorRefinement(sessions, faults, TimeSpan.FromSeconds(10));
        var point = Guid.CreateVersion7();

        refinement.RowWritten(point);
        sessions.Refinements.ShouldBeEmpty();

        refinement.IdentitySet(new MemoryActor("tucker", "qwen"));
        var record = await sessions.FirstRefinementAsync.WaitAsync(Wait);

        record.Id.ShouldBe(point);
        record.Alias.ShouldBe("tucker");
        record.Vendor.ShouldBe("qwen");

        refinement.IdentitySet(new MemoryActor("second", "claude"));
        await Task.Delay(200);

        sessions.Refinements.Count.ShouldBe(1);
        faults.Lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task IdentitySetBeforeRowWrites_RefinesOnceWhenTheRowLands()
    {
        var sessions = new FakeSessionRegistry();
        var faults = new RecordingSessionCaptureFaultSink();
        var refinement = new SessionActorRefinement(sessions, faults, TimeSpan.FromSeconds(10));
        var point = Guid.CreateVersion7();

        refinement.IdentitySet(new MemoryActor("tucker", "qwen"));
        refinement.IdentitySet(new MemoryActor("second", "claude"));
        sessions.Refinements.ShouldBeEmpty();

        refinement.RowWritten(point);
        var record = await sessions.FirstRefinementAsync.WaitAsync(Wait);

        record.Id.ShouldBe(point);
        record.Alias.ShouldBe("tucker");
        record.Vendor.ShouldBe("qwen");

        refinement.RowWritten(Guid.CreateVersion7());
        await Task.Delay(200);

        sessions.Refinements.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RefinementOutage_LogsOneFaultLine_AndNeverRetries()
    {
        var sessions = new FakeSessionRegistry { RefineFailure = new UpstreamUnavailableException("qdrant") };
        var faults = new RecordingSessionCaptureFaultSink();
        var refinement = new SessionActorRefinement(sessions, faults, TimeSpan.FromSeconds(10));

        refinement.RowWritten(Guid.CreateVersion7());
        refinement.IdentitySet(new MemoryActor("tucker", "qwen"));
        var line = await faults.FirstLineAsync.WaitAsync(Wait);

        line.ShouldContain("refinement");

        refinement.IdentitySet(new MemoryActor("tucker-2", "qwen"));
        await Task.Delay(300);

        sessions.Refinements.Count.ShouldBe(1);
        faults.Lines.Count.ShouldBe(1);
    }

    [Fact]
    public async Task BlockedRefinement_IsAbandonedAtTheBudget_AndFaultsOnce()
    {
        var sessions = new FakeSessionRegistry { BlockRefinements = true };
        var faults = new RecordingSessionCaptureFaultSink();
        var refinement = new SessionActorRefinement(sessions, faults, TimeSpan.FromMilliseconds(250));

        refinement.RowWritten(Guid.CreateVersion7());
        refinement.IdentitySet(new MemoryActor("tucker", "qwen"));
        var line = await faults.FirstLineAsync.WaitAsync(TimeSpan.FromSeconds(10));

        line.ShouldContain("write budget");
        faults.Lines.Count.ShouldBe(1);
    }

    [Fact]
    public void RowNeverWritten_PendingIdentityRefinesNothing()
    {
        var sessions = new FakeSessionRegistry();
        var faults = new RecordingSessionCaptureFaultSink();
        var refinement = new SessionActorRefinement(sessions, faults, TimeSpan.FromSeconds(10));

        refinement.IdentitySet(new MemoryActor("tucker", "qwen"));
        refinement.IdentitySet(new MemoryActor("second", "claude"));

        sessions.Refinements.ShouldBeEmpty();
        faults.Lines.ShouldBeEmpty();
    }
}
