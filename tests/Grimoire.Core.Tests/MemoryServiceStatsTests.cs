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
    public async Task Stats_PendingCompaction_SumsClustersAcrossTomes()
    {
        var stale = FakeClock.DefaultStart.AddDays(-30);
        Seed("stale one", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, vector: [1f, 0f, 0f], accessed: stale);
        Seed("stale two", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, vector: [0.97f, 0.24f, 0f], accessed: stale);
        Seed("alpha stale one", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, vector: [0f, 1f, 0f], accessed: stale, tome: "alpha");
        Seed("alpha stale two", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, vector: [0.24f, 0.97f, 0f], accessed: stale, tome: "alpha");

        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.PendingCompaction.ShouldBe(4);
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
        stats.LastWriteByTome.ShouldBeEmpty();
    }

    [Fact]
    public async Task Stats_LastWriteByTome_CarriesExactlyTheByTomeKeys()
    {
        Seed("default active", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active);
        Seed("alpha active", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, tome: "alpha");
        Seed("alpha buried", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Tombstoned, tome: "alpha", tombstoneReason: "gone", tombstonedAt: FakeClock.DefaultStart);
        Seed("beta active", MemoryScope.Global, MemoryKind.Fact, MemoryStatus.Active, tome: "beta");

        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.ByTome.Count.ShouldBe(3);
        stats.LastWriteByTome.Count.ShouldBe(3);
        stats.LastWriteByTome.Keys.ShouldBe(stats.ByTome.Keys);
        stats.ByTome.Keys.ShouldBe(["alpha", "beta", TomeKey.DefaultTome]);
    }

    [Fact]
    public async Task Stats_LastWriteByTome_PatchedOldRecordBeatsAYoungerCreation()
    {
        var oldest = FakeClock.DefaultStart.AddDays(-30);
        var patched = FakeClock.DefaultStart;
        Seed("ancient patched", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, tome: "alpha", created: oldest, corrections: [TestMemories.Patch(patched)]);
        Seed("younger plain", MemoryScope.Global, MemoryKind.Fact, MemoryStatus.Active, tome: "alpha", created: oldest.AddDays(29));

        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.LastWriteByTome["alpha"].ShouldBe(patched);
    }

    [Fact]
    public async Task Stats_LastWriteByTome_LatestOfSeveralCorrectionsWins()
    {
        var created = FakeClock.DefaultStart.AddDays(-10);
        Seed(
            "twice corrected",
            MemoryScope.Project,
            MemoryKind.Journal,
            MemoryStatus.Active,
            tome: "alpha",
            created: created,
            corrections:
            [
                TestMemories.Patch(created.AddDays(1), "first fix"),
                TestMemories.Patch(created.AddDays(4), "second fix"),
            ]);

        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.LastWriteByTome["alpha"].ShouldBe(created.AddDays(4));
    }

    [Fact]
    public async Task Stats_LastWriteByTome_IgnoresARead()
    {
        var created = FakeClock.DefaultStart.AddDays(-10);
        var touched = FakeClock.DefaultStart.AddDays(-1);
        Seed("ancient", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, tome: "alpha", created: created, accessed: touched);
        Seed("newest plain", MemoryScope.Global, MemoryKind.Fact, MemoryStatus.Active, tome: "alpha", created: created.AddDays(1));

        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.LastWriteByTome["alpha"].ShouldBe(created.AddDays(1));
    }

    [Fact]
    public async Task Stats_LastWriteByTome_CountsTheTombstoneAsAWrite()
    {
        Seed("buried", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Tombstoned, tome: "alpha", created: FakeClock.DefaultStart.AddDays(-10), tombstoneReason: "gone", tombstonedAt: FakeClock.DefaultStart.AddDays(-2));
        Seed("plain", MemoryScope.Global, MemoryKind.Fact, MemoryStatus.Active, tome: "alpha", created: FakeClock.DefaultStart.AddDays(-5));

        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.LastWriteByTome["alpha"].ShouldBe(FakeClock.DefaultStart.AddDays(-2));
    }

    [Fact]
    public async Task Stats_LastWriteByTome_SupersededRecordsMoveNothing()
    {
        Seed("active", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, tome: "alpha", created: FakeClock.DefaultStart.AddDays(-3));
        Seed("superseded later", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Superseded, tome: "alpha", created: FakeClock.DefaultStart, supersededBy: Guid.NewGuid());
        Seed("orphan superseded", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Superseded, tome: "gamma", created: FakeClock.DefaultStart, supersededBy: Guid.NewGuid());

        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.LastWriteByTome["alpha"].ShouldBe(FakeClock.DefaultStart.AddDays(-3));
        stats.LastWriteByTome.Keys.ShouldNotContain("gamma");
        stats.LastWriteByTome.Keys.ShouldBe(stats.ByTome.Keys);
    }

    [Fact]
    public async Task Stats_LastWriteByTome_ReadPathLeavesTheCensusUntouched()
    {
        var created = FakeClock.DefaultStart.AddDays(-10);
        var service = TestMemories.Service(
            _store,
            new FakeEmbeddingProvider(new Dictionary<string, float[]> { ["recall me"] = [1f, 0f, 0f] }),
            _clock);
        Seed("recall me", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, created: created);

        var recalled = (await service.RecallAsync(
            new RecallQuery { Text = "recall me", Tome = TomeKey.DefaultTome, Limit = 5 },
            CancellationToken.None)).Matches;

        recalled.ShouldNotBeEmpty();
        _clock.Advance(TimeSpan.FromDays(30));
        var stats = await service.StatsAsync(CancellationToken.None);

        stats.LastWriteByTome[TomeKey.DefaultTome].ShouldBe(created);
    }

    [Fact]
    public async Task Stats_LastWriteByTome_TombstoneWithoutAStampFallsBackToCreation()
    {
        var created = FakeClock.DefaultStart.AddDays(-7);
        Seed("legacy buried", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Tombstoned, tome: "alpha", created: created, tombstoneReason: "gone");

        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.ByTome["alpha"].Tombstoned.ShouldBe(1);
        stats.LastWriteByTome["alpha"].ShouldBe(created);
    }

    [Fact]
    public async Task PendingCompactionByTome_SplitsStaleClustersPerTome()
    {
        var stale = FakeClock.DefaultStart.AddDays(-30);
        Seed("alpha stale one", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, vector: [1f, 0f, 0f], accessed: stale, tome: "alpha");
        Seed("alpha stale two", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, vector: [0.97f, 0.24f, 0f], accessed: stale, tome: "alpha");
        Seed("beta stale one", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, vector: [1f, 0f, 0f], accessed: stale, tome: "beta");
        Seed("beta stale two", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, vector: [0.97f, 0.24f, 0f], accessed: stale, tome: "beta");
        Seed("home stale one", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, vector: [1f, 0f, 0f], accessed: stale);
        Seed("home stale two", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, vector: [0.97f, 0.24f, 0f], accessed: stale);
        Seed("gamma fresh", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, vector: [1f, 0f, 0f], tome: "gamma");

        var pending = await _service.PendingCompactionByTomeAsync(CancellationToken.None);

        pending.Keys.ShouldBe(["alpha", "beta", TomeKey.DefaultTome, "gamma"]);
        pending["alpha"].ShouldBe(2);
        pending["beta"].ShouldBe(2);
        pending[TomeKey.DefaultTome].ShouldBe(2);
        pending["gamma"].ShouldBe(0);
    }

    [Fact]
    public async Task PendingCompactionByTome_EmptyStore_IsEmpty()
    {
        var pending = await _service.PendingCompactionByTomeAsync(CancellationToken.None);

        pending.ShouldBeEmpty();
    }

    [Fact]
    public async Task PendingCompactionByTome_SkipsTomesWithOnlyInactiveRecords()
    {
        Seed("alpha buried", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Tombstoned, vector: [1f, 0f, 0f], tome: "alpha", tombstoneReason: "gone", tombstonedAt: FakeClock.DefaultStart);
        Seed("home stale one", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, vector: [1f, 0f, 0f], accessed: FakeClock.DefaultStart.AddDays(-30));
        Seed("home stale two", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, vector: [0.97f, 0.24f, 0f], accessed: FakeClock.DefaultStart.AddDays(-30));

        var pending = await _service.PendingCompactionByTomeAsync(CancellationToken.None);

        pending.Keys.ShouldBe([TomeKey.DefaultTome]);
    }

    [Fact]
    public async Task Stats_PaysThreeListRoundTripsOnTheCensusAndConflictPath()
    {
        Seed("default active", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active);
        Seed("alpha active", MemoryScope.Project, MemoryKind.Journal, MemoryStatus.Active, tome: "alpha");

        var stats = await _service.StatsAsync(CancellationToken.None);

        stats.ByTome.Count.ShouldBe(2);
        // Two census scrolls plus one idempotency probe. The stats path is the heaviest
        // full-store read in the product; a fourth call here means someone re-widened it.
        _store.ListCallCount.ShouldBe(3);
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
        string? tome = null,
        IReadOnlyList<CorrectionEntry>? corrections = null)
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
            tome: tome,
            corrections: corrections);
        _store.Seed(record, memoryVector);
    }
}
