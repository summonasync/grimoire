using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class GcSweeperTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    [Fact]
    public async Task Sweep_RemovesExpiredSessionRecords()
    {
        var store = new FakeMemoryStore();
        store.Seed("stale session observation", [1f, 0f], scope: MemoryScope.Session, expiresAt: FakeClock.DefaultStart.AddHours(-1));
        var sweeper = CreateSweeper(store);
        using var cts = new CancellationTokenSource();

        await sweeper.StartAsync(cts.Token);
        await Wait.UntilAsync(() => store.Records.Count == 0);
        await sweeper.StopAsync(cts.Token);

        store.Records.ShouldBeEmpty();
        store.DeleteCount.ShouldBe(1);
    }

    [Fact]
    public async Task Sweep_RemovesAgedTombstones()
    {
        var store = new FakeMemoryStore();
        store.Seed(
            "old tombstone",
            [1f, 0f],
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "mistake",
            tombstonedAt: FakeClock.DefaultStart.AddDays(-31));
        var sweeper = CreateSweeper(store);
        using var cts = new CancellationTokenSource();

        await sweeper.StartAsync(cts.Token);
        await Wait.UntilAsync(() => store.Records.Count == 0);
        await sweeper.StopAsync(cts.Token);

        store.DeleteCount.ShouldBe(1);
    }

    [Fact]
    public async Task Sweep_WaitsConfiguredIntervalBetweenRuns()
    {
        var observedDelays = new List<TimeSpan>();
        var store = new FakeMemoryStore();
        store.Seed("stale session observation", [1f, 0f], scope: MemoryScope.Session, expiresAt: FakeClock.DefaultStart.AddHours(-1));
        var sweeper = new GcSweeper(
            CreateService(store).GcAsync,
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
    public async Task Sweep_SurvivesTransientStoreFailure()
    {
        var store = new FlakyMemoryStore();
        store.Seed("stale session observation", [1f, 0f], scope: MemoryScope.Session, expiresAt: FakeClock.DefaultStart.AddHours(-1));
        var sweeper = new GcSweeper(CreateService(store).GcAsync, new FakeClock(), Interval, pause: (_, _) => Task.CompletedTask);
        using var cts = new CancellationTokenSource();

        await sweeper.StartAsync(cts.Token);
        await Wait.UntilAsync(() => store.Records.Count == 0);
        await sweeper.StopAsync(cts.Token);

        store.ListAttempts.ShouldBeGreaterThanOrEqualTo(2);
        store.DeleteCount.ShouldBe(1);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveInterval() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new GcSweeper(_ => Task.FromResult(new GcResult { ExpiredSessions = 0, AgedTombstones = 0 }), new FakeClock(), TimeSpan.Zero));

    private static GcSweeper CreateSweeper(FakeMemoryStore store) =>
        new(CreateService(store).GcAsync, new FakeClock(), Interval, pause: (_, _) => Task.CompletedTask);

    private static MemoryService CreateService(FakeMemoryStore store) =>
        new(store, new FakeEmbeddingProvider(), new FakeClock());

    private sealed class FlakyMemoryStore : FakeMemoryStore
    {
        private int _listFailuresLeft = 1;

        public int ListAttempts { get; private set; }

        public override Task<IReadOnlyList<MemoryRecord>> ListAsync(MemoryFilter filter, CancellationToken cancellationToken)
        {
            ListAttempts++;
            if (_listFailuresLeft-- > 0)
            {
                throw new IOException("qdrant is unreachable");
            }

            return base.ListAsync(filter, cancellationToken);
        }
    }
}
