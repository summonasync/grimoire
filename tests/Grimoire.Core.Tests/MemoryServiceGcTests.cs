using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryServiceGcTests
{
    private readonly FakeMemoryStore _store = new();

    private readonly FakeClock _clock = new();

    private readonly MemoryService _service;

    public MemoryServiceGcTests()
    {
        _service = TestMemories.Service(
            _store,
            new FakeEmbeddingProvider(new Dictionary<string, float[]>()),
            _clock);
    }

    [Fact]
    public async Task Gc_RemovesOnlyExpiredSessionPoints()
    {
        var expired = Seed(scope: MemoryScope.Session, expiresAt: _clock.UtcNow.AddHours(-1));
        var live = Seed(scope: MemoryScope.Session, expiresAt: _clock.UtcNow.AddHours(5));
        var longTerm = Seed(scope: MemoryScope.Project);

        var result = await _service.GcAsync(CancellationToken.None);

        result.ExpiredSessions.ShouldBe(1);
        result.AgedTombstones.ShouldBe(0);
        result.TotalRemoved.ShouldBe(1);
        _store.Records.ShouldNotContain(record => record.Id == expired.Id);
        _store.Records.ShouldContain(record => record.Id == live.Id);
        _store.Records.ShouldContain(record => record.Id == longTerm.Id);
    }

    [Fact]
    public async Task Gc_CollectsSessionExpiringExactlyNow()
    {
        Seed(scope: MemoryScope.Session, expiresAt: _clock.UtcNow);

        var result = await _service.GcAsync(CancellationToken.None);

        result.ExpiredSessions.ShouldBe(1);
        _store.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task Gc_RemovesTombstonesOlderThanRetention()
    {
        var aged = Seed(
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "forgotten",
            tombstonedAt: _clock.UtcNow.AddDays(-31));

        var result = await _service.GcAsync(CancellationToken.None);

        result.AgedTombstones.ShouldBe(1);
        _store.Records.ShouldNotContain(record => record.Id == aged.Id);
    }

    [Fact]
    public async Task Gc_KeepsTombstonesWithinRetention()
    {
        var recent = Seed(
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "forgotten",
            tombstonedAt: _clock.UtcNow.AddDays(-29));

        var result = await _service.GcAsync(CancellationToken.None);

        result.AgedTombstones.ShouldBe(0);
        _store.Records.ShouldContain(record => record.Id == recent.Id);
    }

    [Fact]
    public async Task Gc_NeverTouchesActiveOrSupersededLongTermRecords()
    {
        var active = Seed(scope: MemoryScope.Global);
        var superseded = Seed(scope: MemoryScope.Global, status: MemoryStatus.Superseded, supersededBy: Guid.NewGuid());

        var result = await _service.GcAsync(CancellationToken.None);

        result.TotalRemoved.ShouldBe(0);
        _store.Records.ShouldContain(record => record.Id == active.Id);
        _store.Records.ShouldContain(record => record.Id == superseded.Id);
    }

    [Fact]
    public async Task Gc_EmptyStore_RemovesNothing()
    {
        var result = await _service.GcAsync(CancellationToken.None);

        result.ExpiredSessions.ShouldBe(0);
        result.AgedTombstones.ShouldBe(0);
        _store.DeleteCount.ShouldBe(0);
    }

    private MemoryRecord Seed(
        MemoryScope scope = MemoryScope.Project,
        MemoryStatus status = MemoryStatus.Active,
        DateTimeOffset? expiresAt = null,
        Guid? supersededBy = null,
        string? tombstoneReason = null,
        DateTimeOffset? tombstonedAt = null)
    {
        var record = TestMemories.Create(
            "note",
            [1f, 0f, 0f],
            scope: scope,
            status: status,
            expiresAt: expiresAt,
            supersededBy: supersededBy,
            tombstoneReason: tombstoneReason,
            tombstonedAt: tombstonedAt);
        _store.Seed(record, [1f, 0f, 0f]);
        return record;
    }
}
