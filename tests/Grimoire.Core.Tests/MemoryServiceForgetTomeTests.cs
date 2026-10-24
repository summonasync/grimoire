using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryServiceForgetTomeTests
{
    private readonly FakeMemoryStore _store = new();

    private readonly FakeClock _clock = new();

    private readonly MemoryService _service;

    private readonly CorrectionService _corrections;

    public MemoryServiceForgetTomeTests()
    {
        _service = TestMemories.Service(
            _store,
            new FakeEmbeddingProvider(new Dictionary<string, float[]>()),
            _clock);
        _corrections = TestMemories.Corrections(_store, _clock);
    }

    [Fact]
    public async Task ForgetTome_TombstonesEveryActiveRecordAndReturnsCount()
    {
        var first = Seed(tome: "alpha");
        var second = Seed(tome: "alpha");
        var other = Seed(tome: "beta");
        _clock.Advance(TimeSpan.FromHours(1));

        var result = await _service.ForgetTomeAsync("alpha", "tome retired", CancellationToken.None, TestMemories.Agent);

        result.Forgotten.ShouldBe(2);
        foreach (var record in new[] { first, second })
        {
            var stored = (await _store.GetAsync(record.Id, CancellationToken.None))!;
            stored.Status.ShouldBe(MemoryStatus.Tombstoned);
            stored.TombstoneReason.ShouldBe("tome retired");
            stored.TombstonedAt.ShouldBe(_clock.UtcNow);
        }

        _store.Records.Single(record => record.Id == other.Id).Status.ShouldBe(MemoryStatus.Active);
        _store.DeleteCount.ShouldBe(0);
    }

    [Fact]
    public async Task ForgetTome_AppendsTrailLinesWithReasonAndActor()
    {
        var record = Seed(tome: "alpha");
        _clock.Advance(TimeSpan.FromHours(1));

        await _service.ForgetTomeAsync("Alpha", "tome retired", CancellationToken.None, TestMemories.Agent);

        var line = (await _store.GetAsync(record.Id, CancellationToken.None))!.Corrections.Single();
        line.Action.ShouldBe(CorrectionAction.Forget);
        line.Reason.ShouldBe("tome retired");
        line.Actor.ShouldBe(TestMemories.Agent);
        line.At.ShouldBe(_clock.UtcNow);
    }

    [Fact]
    public async Task ForgetTome_LeavesAlreadyTombstonedRecordsUntouched()
    {
        Seed(tome: "alpha");
        var buried = Seed(
            tome: "alpha",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "original reason",
            tombstonedAt: FakeClock.DefaultStart);
        _clock.Advance(TimeSpan.FromDays(2));

        var result = await _service.ForgetTomeAsync("alpha", "tome retired", CancellationToken.None);

        result.Forgotten.ShouldBe(1);
        var storedBuried = (await _store.GetAsync(buried.Id, CancellationToken.None))!;
        storedBuried.Status.ShouldBe(MemoryStatus.Tombstoned);
        storedBuried.TombstoneReason.ShouldBe("original reason");
        storedBuried.TombstonedAt.ShouldBe(FakeClock.DefaultStart);
        storedBuried.Corrections.ShouldBe([]);
    }

    [Fact]
    public async Task ForgetTome_LeavesSupersededRecordsUntouched()
    {
        Seed(tome: "alpha");
        var superseded = Seed(tome: "alpha", status: MemoryStatus.Superseded, supersededBy: Guid.NewGuid());

        var result = await _service.ForgetTomeAsync("alpha", "tome retired", CancellationToken.None);

        result.Forgotten.ShouldBe(1);
        var storedSuperseded = (await _store.GetAsync(superseded.Id, CancellationToken.None))!;
        storedSuperseded.Status.ShouldBe(MemoryStatus.Superseded);
        storedSuperseded.Corrections.ShouldBe([]);
    }

    [Fact]
    public async Task ForgetTome_EmptyTome_ReturnsZeroWithoutWrites()
    {
        var result = await _service.ForgetTomeAsync("ghost", "tome retired", CancellationToken.None);

        result.Forgotten.ShouldBe(0);
        _store.UpdateCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ForgetTome_MissingReason_ThrowsAndLeavesStoreUntouched(string reason)
    {
        var record = Seed(tome: "alpha");

        await Should.ThrowAsync<ArgumentException>(
            () => _service.ForgetTomeAsync("alpha", reason, CancellationToken.None));

        _store.UpdateCount.ShouldBe(0);
        _store.Records.Single(stored => stored.Id == record.Id).Status.ShouldBe(MemoryStatus.Active);
    }

    [Theory]
    [InlineData("Not Valid!!")]
    [InlineData("   ")]
    public async Task ForgetTome_InvalidTomeKey_ThrowsAndLeavesStoreUntouched(string tome)
    {
        Seed(tome: "alpha");

        await Should.ThrowAsync<ArgumentException>(
            () => _service.ForgetTomeAsync(tome, "tome retired", CancellationToken.None));

        _store.UpdateCount.ShouldBe(0);
        _store.Records.ShouldAllBe(record => record.Status == MemoryStatus.Active);
    }

    [Fact]
    public async Task ForgetTome_RemovesTheTomeFromTheActiveCensusUntilRestore()
    {
        var first = Seed(tome: "alpha");
        Seed(tome: "alpha");
        Seed(tome: "beta");

        await _service.ForgetTomeAsync("alpha", "tome retired", CancellationToken.None);

        var stats = await _service.StatsAsync(CancellationToken.None);
        stats.ByTome["alpha"].Active.ShouldBe(0);
        stats.ByTome["alpha"].Tombstoned.ShouldBe(2);

        var restored = await _corrections.RestoreAsync(first.Id, "second thoughts", CancellationToken.None, "alpha");

        restored.Restored.ShouldBeTrue();
        var after = await _service.StatsAsync(CancellationToken.None);
        after.ByTome["alpha"].Active.ShouldBe(1);
        after.ByTome["alpha"].Tombstoned.ShouldBe(1);
    }

    [Fact]
    public async Task ForgetTome_TombstonesStayUntilGcAgesThemOutOfTheCensus()
    {
        var record = Seed(tome: "alpha");

        await _service.ForgetTomeAsync("alpha", "tome retired", CancellationToken.None);
        _clock.Advance(TimeSpan.FromDays(31));

        var collected = await _service.GcAsync(CancellationToken.None);

        collected.AgedTombstones.ShouldBe(1);
        _store.Records.ShouldNotContain(stored => stored.Id == record.Id);
        var stats = await _service.StatsAsync(CancellationToken.None);
        stats.ByTome.ShouldNotContainKey("alpha");
    }

    private MemoryRecord Seed(
        string? tome = null,
        MemoryStatus status = MemoryStatus.Active,
        Guid? supersededBy = null,
        string? tombstoneReason = null,
        DateTimeOffset? tombstonedAt = null)
    {
        var record = TestMemories.Create(
            "deploy on friday",
            [1f, 0f, 0f],
            status: status,
            supersededBy: supersededBy,
            tombstoneReason: tombstoneReason,
            tombstonedAt: tombstonedAt,
            tome: tome);
        _store.Seed(record, [1f, 0f, 0f]);
        return record;
    }
}
