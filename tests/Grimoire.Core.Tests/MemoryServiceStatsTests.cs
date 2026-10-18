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
        foreach (var pair in stats.ActiveByScope)
        {
            pair.Value.ShouldBe(0);
        }
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

    private void Seed(
        string content,
        MemoryScope scope,
        MemoryKind kind,
        MemoryStatus status,
        float[]? vector = null,
        DateTimeOffset? accessed = null,
        Guid? supersededBy = null,
        string? tombstoneReason = null,
        DateTimeOffset? tombstonedAt = null)
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
            tombstonedAt: tombstonedAt);
        _store.Seed(record, memoryVector);
    }
}
