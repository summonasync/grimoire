using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class ConsolidationSweeperTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    [Fact]
    public async Task Sweep_RecordsHeartbeatAfterSuccess()
    {
        var store = new FakeMemoryStore();
        var state = new FakeConsolidationStateStore();
        var sweeper = new ConsolidationSweeper(
            SweepOf(store),
            new FakeClock(),
            Interval,
            state,
            pause: (_, _) => Task.CompletedTask);
        using var cts = new CancellationTokenSource();

        await sweeper.StartAsync(cts.Token);
        await Wait.UntilAsync(() => state.Writes.Count > 0);
        await sweeper.StopAsync(cts.Token);

        var heartbeat = state.Writes[0];
        heartbeat.IsUnknown.ShouldBeFalse();
        heartbeat.LastAttemptAt.ShouldBe(FakeClock.DefaultStart);
        heartbeat.LastSuccessAt.ShouldBe(FakeClock.DefaultStart);
        heartbeat.ConsolidatedClusters.ShouldBe(0);
        heartbeat.LastError.ShouldBeNull();
    }

    [Fact]
    public async Task Sweep_WaitsConfiguredIntervalBetweenRuns()
    {
        var observedDelays = new List<TimeSpan>();
        var sweeper = new ConsolidationSweeper(
            _ => Task.FromResult(new ConsolidationResult { Clusters = 0 }),
            new FakeClock(),
            Interval,
            pause: (delay, _) =>
            {
                observedDelays.Add(delay);
                return Task.CompletedTask;
            });
        using var cts = new CancellationTokenSource();

        await sweeper.StartAsync(cts.Token);
        await Wait.UntilAsync(() => observedDelays.Count >= 2);
        await sweeper.StopAsync(cts.Token);

        observedDelays.Take(2).ShouldBe([Interval, Interval]);
    }

    [Fact]
    public async Task Sweep_FailureRecordsLastErrorAndKeepsThePreviousSuccess()
    {
        var store = new FlakyMemoryStore();
        var state = new FakeConsolidationStateStore(new ConsolidationState
        {
            LastAttemptAt = FakeClock.DefaultStart.AddHours(-12),
            LastSuccessAt = FakeClock.DefaultStart.AddHours(-12),
            ConsolidatedClusters = 2,
        });
        var firstSweep = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondSweep = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateIndex = 0;
        var gates = new[] { firstSweep.Task, secondSweep.Task, Task.CompletedTask };
        var sweeper = new ConsolidationSweeper(
            SweepOf(store),
            new FakeClock(),
            Interval,
            state,
            pause: (_, _) => gates[Math.Min(gateIndex++, gates.Length - 1)]);
        using var cts = new CancellationTokenSource();

        await sweeper.StartAsync(cts.Token);
        firstSweep.SetResult();
        await Wait.UntilAsync(() => state.Writes.Count > 0);

        var failure = state.Writes[0];
        failure.LastAttemptAt.ShouldBe(FakeClock.DefaultStart);
        failure.LastSuccessAt.ShouldBe(FakeClock.DefaultStart.AddHours(-12));
        failure.ConsolidatedClusters.ShouldBe(2);
        failure.LastError.ShouldBe("qdrant is unreachable");

        secondSweep.SetResult();
        await Wait.UntilAsync(() => state.Writes.Count >= 2);
        await sweeper.StopAsync(cts.Token);

        var recovered = state.Writes[1];
        recovered.LastError.ShouldBeNull();
        recovered.LastSuccessAt.ShouldBe(FakeClock.DefaultStart);
        recovered.ConsolidatedClusters.ShouldBe(0);
    }

    [Fact]
    public async Task Sweep_KeepsSweepingWhenTheHeartbeatWriteFails()
    {
        var store = new FakeMemoryStore();
        var state = new FakeConsolidationStateStore { FailWrites = true };
        var sweeper = new ConsolidationSweeper(
            SweepOf(store),
            new FakeClock(),
            Interval,
            state,
            pause: (_, _) => Task.CompletedTask);
        using var cts = new CancellationTokenSource();

        await sweeper.StartAsync(cts.Token);
        await Wait.UntilAsync(() => state.WriteAttempts >= 2);
        await sweeper.StopAsync(cts.Token);

        state.Writes.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_RejectsNonPositiveInterval() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new ConsolidationSweeper(_ => Task.FromResult(new ConsolidationResult { Clusters = 0 }), new FakeClock(), TimeSpan.Zero));

    private static Func<CancellationToken, Task<ConsolidationResult>> SweepOf(FakeMemoryStore store)
    {
        var service = new MemoryService(store, new FakeEmbeddingProvider(), new FakeClock());
        return cancellationToken => service.ConsolidateAsync(MemoryActor.Agent("grimoire", "consolidator"), cancellationToken);
    }

    private sealed class FlakyMemoryStore : FakeMemoryStore
    {
        private int _listFailuresLeft = 1;

        public override Task<IReadOnlyList<MemoryRecord>> ListAsync(MemoryFilter filter, CancellationToken cancellationToken)
        {
            if (_listFailuresLeft-- > 0)
            {
                throw new IOException("qdrant is unreachable");
            }

            return base.ListAsync(filter, cancellationToken);
        }
    }
}
