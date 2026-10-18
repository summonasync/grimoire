using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryServiceStatsTests
{
    private readonly FakeMemoryStore _store = new();

    private readonly FakeClock _clock = new();

    private readonly MemoryService _service;

    public MemoryServiceStatsTests()
    {
        _service = TestMemories.Service(
            _store,
            new FakeEmbeddingProvider(new Dictionary<string, float[]>()),
            _clock);
    }

    [Fact]
    public async Task Stats_EmptyStore_ReportsZeros()
    {
        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.Active.ShouldBe(0);
        stats.Superseded.ShouldBe(0);
        stats.Tombstoned.ShouldBe(0);
        stats.PendingCompaction.ShouldBe(0);
        stats.IdempotencyConflicts.ShouldBe(0);
        stats.Gc.IsUnknown.ShouldBeTrue();
        stats.Gc.UnknownReason.ShouldBe("gc state store is not configured");
        stats.Gc.Stale.ShouldBeTrue();
        foreach (var pair in stats.ActiveByScope)
        {
            pair.Value.ShouldBe(0);
        }
    }

    [Fact]
    public async Task Stats_ReportsTheGcHeartbeatFreshEnoughToNotBeStale()
    {
        var gcState = new FakeGcStateStore(new GcState
        {
            LastAttemptAt = _clock.UtcNow.AddMinutes(-10),
            LastSuccessAt = _clock.UtcNow.AddMinutes(-5),
            RemovedExpired = 2,
            RemovedTombstones = 1,
        });
        var service = TestMemories.Service(_store, new FakeEmbeddingProvider(new Dictionary<string, float[]>()), _clock, gcOptions: new GcOptions { SweepInterval = TimeSpan.FromMinutes(30) }, gcState: gcState);

        var stats = await service.StatsAsync(CancellationToken.None);

        stats.Gc.IsUnknown.ShouldBeFalse();
        stats.Gc.LastSuccessAt.ShouldBe(_clock.UtcNow.AddMinutes(-5));
        stats.Gc.RemovedExpired.ShouldBe(2);
        stats.Gc.RemovedTombstones.ShouldBe(1);
        stats.Gc.LastError.ShouldBeNull();
        stats.Gc.Stale.ShouldBeFalse();
    }

    [Fact]
    public async Task Stats_FlagsTheGcHeartbeatStalePastTwiceTheSweepInterval()
    {
        var gcState = new FakeGcStateStore(new GcState
        {
            LastAttemptAt = _clock.UtcNow.AddHours(-3),
            LastSuccessAt = _clock.UtcNow.AddHours(-3),
        });
        var service = TestMemories.Service(_store, new FakeEmbeddingProvider(new Dictionary<string, float[]>()), _clock, gcOptions: new GcOptions { SweepInterval = TimeSpan.FromMinutes(30) }, gcState: gcState);

        var stats = await service.StatsAsync(CancellationToken.None);

        stats.Gc.Stale.ShouldBeTrue();
    }

    [Fact]
    public async Task Stats_SurfacesAnUnreadableHeartbeatAsUnknownButNotACrash()
    {
        var gcState = new FakeGcStateStore(GcState.Unknown("gc state file is not valid JSON: boom"));
        var service = TestMemories.Service(_store, new FakeEmbeddingProvider(new Dictionary<string, float[]>()), _clock, gcOptions: new GcOptions(), gcState: gcState);

        var stats = await service.StatsAsync(CancellationToken.None);

        stats.Gc.IsUnknown.ShouldBeTrue();
        stats.Gc.UnknownReason.ShouldBe("gc state file is not valid JSON: boom");
        stats.Gc.Stale.ShouldBeTrue();
    }

    [Fact]
    public async Task Stats_CountsRecordsByStatus()
    {
        Seed("active project", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active);
        Seed("active global", MemoryScope.Global, MemoryKind.Journal, MemoryStatus.Active);
        Seed("superseded", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Superseded, supersededBy: Guid.NewGuid());
        Seed("tombstoned", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Tombstoned, tombstoneReason: "forgotten", tombstonedAt: FakeClock.DefaultStart);

        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.Active.ShouldBe(2);
        stats.Superseded.ShouldBe(1);
        stats.Tombstoned.ShouldBe(1);
    }

    [Fact]
    public async Task Stats_BreaksActiveDownByScope()
    {
        Seed("project", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active);
        Seed("global", MemoryScope.Global, MemoryKind.Fact, MemoryStatus.Active);
        Seed("personal", MemoryScope.Personal, MemoryKind.Fact, MemoryStatus.Active);
        Seed("session", MemoryScope.Session, MemoryKind.Journal, MemoryStatus.Active);
        Seed("tombstoned personal", MemoryScope.Personal, MemoryKind.Journal, MemoryStatus.Tombstoned, tombstoneReason: "forgotten", tombstonedAt: FakeClock.DefaultStart);

        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.ActiveByScope[MemoryScope.Project].ShouldBe(1);
        stats.ActiveByScope[MemoryScope.Global].ShouldBe(1);
        stats.ActiveByScope[MemoryScope.Personal].ShouldBe(1);
        stats.ActiveByScope[MemoryScope.Session].ShouldBe(1);
    }

    [Fact]
    public async Task Stats_PendingCompactionCountsStaleClusteredJournalOnly()
    {
        var stale = FakeClock.DefaultStart.AddDays(-30);
        Seed("stale one", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, vector: [1f, 0f, 0f], accessed: stale);
        Seed("stale two", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, vector: [0.97f, 0.24f, 0f], accessed: stale);
        Seed("fresh journal", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, vector: [0.94f, 0.34f, 0f], accessed: FakeClock.DefaultStart);
        Seed("stale fact", MemoryScope.Project, MemoryKind.Fact, MemoryStatus.Active, vector: [0.9f, 0.4359f, 0f], accessed: stale);
        Seed("stale session", MemoryScope.Session, MemoryKind.Journal, MemoryStatus.Active, vector: [0.8f, 0.6f, 0f], accessed: stale);
        Seed("tombstoned journal", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Tombstoned, vector: [0.7f, 0.714f, 0f], tombstoneReason: "forgotten", tombstonedAt: FakeClock.DefaultStart);

        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.PendingCompaction.ShouldBe(2);
    }

    [Fact]
    public async Task Stats_PendingCompactionIsZeroWithoutClusters()
    {
        var stale = FakeClock.DefaultStart.AddDays(-30);
        Seed("build times", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, vector: [1f, 0f, 0f], accessed: stale);
        Seed("coffee tastes", MemoryScope.Global, MemoryKind.Journal, MemoryStatus.Active, vector: [0f, 1f, 0f], accessed: stale);

        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.PendingCompaction.ShouldBe(0);
    }

    [Fact]
    public async Task Stats_IdempotencyConflicts_ZeroOnHealthyStore()
    {
        Seed("original", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, idempotencyKey: "key-a");
        Seed("unrelated", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, idempotencyKey: "key-b");
        Seed("keyless", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active);

        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.IdempotencyConflicts.ShouldBe(0);
    }

    [Fact]
    public async Task Stats_IdempotencyConflicts_CountsOrphansOfDuplicateGroup()
    {
        Seed("original", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, idempotencyKey: "key-a", created: FakeClock.DefaultStart);
        Seed("orphan one", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, idempotencyKey: "key-a", created: FakeClock.DefaultStart.AddMinutes(1));
        Seed("orphan two", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, idempotencyKey: "key-a", created: FakeClock.DefaultStart.AddMinutes(2));
        Seed("healthy other", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, idempotencyKey: "key-b", created: FakeClock.DefaultStart.AddMinutes(3));

        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.IdempotencyConflicts.ShouldBe(2);
    }

    [Fact]
    public async Task Stats_IdempotencyConflicts_IgnoresInactiveAndStaleRecords()
    {
        Seed("active original", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, idempotencyKey: "key-a");
        Seed("superseded twin", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Superseded, idempotencyKey: "key-a", supersededBy: Guid.NewGuid());
        Seed("stale orphan", MemoryScope.Global, MemoryKind.Journal, MemoryStatus.Active, idempotencyKey: "key-a", created: FakeClock.DefaultStart.AddDays(-2));

        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.IdempotencyConflicts.ShouldBe(0);
    }

    [Fact]
    public async Task Stats_ByTome_CountsActiveAndTombstonedPerKey()
    {
        Seed("default active", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active);
        Seed("alpha active", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, tome: "alpha");
        Seed("alpha second", MemoryScope.Global, MemoryKind.Fact, MemoryStatus.Active, tome: "alpha");
        Seed("alpha buried", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Tombstoned, tome: "alpha", tombstoneReason: "gone", tombstonedAt: FakeClock.DefaultStart);

        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.ByTome.Keys.ShouldBe(["alpha", TomeKey.DefaultTome]);
        stats.ByTome[TomeKey.DefaultTome].Active.ShouldBe(1);
        stats.ByTome[TomeKey.DefaultTome].Tombstoned.ShouldBe(0);
        stats.ByTome["alpha"].Active.ShouldBe(2);
        stats.ByTome["alpha"].Tombstoned.ShouldBe(1);
    }

    [Fact]
    public async Task Stats_ByTome_EmptyStore_HasNoEntries()
    {
        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.ByTome.ShouldBeEmpty();
    }

    private void Seed(
        string content,
        MemoryScope scope,
        MemoryKind kind,
        MemoryStatus status,
        float[]? vector = null,
        DateTimeOffset? accessed = null,
        Guid? supersededBy = null,
        string? tombstoneReason = null,
        DateTimeOffset? tombstonedAt = null,
        string? idempotencyKey = null,
        DateTimeOffset? created = null,
        string? tome = null)
    {
        var memoryVector = vector ?? [1f, 0f, 0f];
        var record = TestMemories.Create(
            content,
            memoryVector,
            scope: scope,
            kind: kind,
            accessed: accessed,
            status: status,
            supersededBy: supersededBy,
            tombstoneReason: tombstoneReason,
            tombstonedAt: tombstonedAt,
            idempotencyKey: idempotencyKey,
            created: created,
            tome: tome);
        _store.Seed(record, memoryVector);
    }
}
