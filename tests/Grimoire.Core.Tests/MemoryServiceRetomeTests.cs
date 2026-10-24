using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryServiceRetomeTests
{
    private readonly FakeMemoryStore _store = new();

    private readonly FakeClock _clock = new();

    private readonly MemoryService _service;

    public MemoryServiceRetomeTests()
    {
        _service = TestMemories.Service(
            _store,
            new FakeEmbeddingProvider(new Dictionary<string, float[]>()),
            _clock);
    }

    [Fact]
    public async Task Retome_ByFilter_MovesAllActiveRecordsAndReturnsCount()
    {
        var first = Seed(tome: "alpha");
        var second = Seed(tome: "alpha");
        var other = Seed(tome: "beta");

        var result = await _service.RetomeAsync(
            new MemoryFilter { Tome = "alpha" },
            "gamma",
            CancellationToken.None,
            TestMemories.Agent);

        result.Retomed.ShouldBe(2);
        _store.Records.Single(record => record.Id == first.Id).Tome.ShouldBe("gamma");
        _store.Records.Single(record => record.Id == second.Id).Tome.ShouldBe("gamma");
        _store.Records.Single(record => record.Id == other.Id).Tome.ShouldBe("beta");
        _store.Records.Count(record => record.Tome == "alpha").ShouldBe(0);
    }

    [Fact]
    public async Task Retome_ByFilter_IgnoresCallerSuppliedLimit()
    {
        var first = Seed(tome: "alpha");
        var second = Seed(tome: "alpha");

        var result = await _service.RetomeAsync(
            new MemoryFilter { Tome = "alpha", Limit = 1 },
            "gamma",
            CancellationToken.None,
            TestMemories.Agent);

        result.Retomed.ShouldBe(2);
        _store.Records.Single(record => record.Id == first.Id).Tome.ShouldBe("gamma");
        _store.Records.Single(record => record.Id == second.Id).Tome.ShouldBe("gamma");
    }

    [Fact]
    public async Task Retome_ByIdList_MovesOnlyListedRecords()
    {
        var moved = Seed(tome: "alpha");
        var movedToo = Seed(tome: "alpha");
        var kept = Seed(tome: "alpha");

        var result = await _service.RetomeAsync(
            [moved.Id, movedToo.Id],
            "beta",
            CancellationToken.None,
            TestMemories.Agent);

        result.Retomed.ShouldBe(2);
        _store.Records.Single(record => record.Id == moved.Id).Tome.ShouldBe("beta");
        _store.Records.Single(record => record.Id == movedToo.Id).Tome.ShouldBe("beta");
        _store.Records.Single(record => record.Id == kept.Id).Tome.ShouldBe("alpha");
    }

    [Fact]
    public async Task Retome_AppendsProvenanceLineAndPreservesExistingTrail()
    {
        var record = Seed(
            tome: "alpha",
            corrections: [TestMemories.Patch(FakeClock.DefaultStart)]);
        _clock.Advance(TimeSpan.FromHours(1));

        await _service.RetomeAsync(
            new MemoryFilter { Tome = "alpha" },
            "beta",
            CancellationToken.None,
            TestMemories.Agent);

        var stored = (await _store.GetAsync(record.Id, CancellationToken.None))!;
        stored.Corrections.Count.ShouldBe(2);
        stored.Corrections[0].Action.ShouldBe(CorrectionAction.Patch);
        var line = stored.Corrections[1];
        line.Action.ShouldBe(CorrectionAction.Retome);
        line.Reason.ShouldBe("tome: alpha → beta");
        line.Actor.ShouldBe(TestMemories.Agent);
        line.At.ShouldBe(_clock.UtcNow);
    }

    [Fact]
    public async Task Retome_ReRunReturnsZeroAndChangesNothing()
    {
        var record = Seed(tome: "alpha");

        await _service.RetomeAsync(
            new MemoryFilter { Tome = "alpha" },
            "beta",
            CancellationToken.None,
            TestMemories.Agent);
        var updatesAfterFirstRun = _store.UpdateCount;

        var rerun = await _service.RetomeAsync(
            new MemoryFilter { Tome = "alpha" },
            "beta",
            CancellationToken.None,
            TestMemories.Agent);

        rerun.Retomed.ShouldBe(0);
        _store.UpdateCount.ShouldBe(updatesAfterFirstRun);
        (await _store.GetAsync(record.Id, CancellationToken.None))!
            .Corrections.Single(line => line.Action == CorrectionAction.Retome);
    }

    [Theory]
    [InlineData("Not Valid!!")]
    [InlineData("   ")]
    public async Task Retome_InvalidTargetKey_ThrowsAndLeavesStoreUntouched(string targetTome)
    {
        Seed(tome: "alpha");

        await Should.ThrowAsync<ArgumentException>(() => _service.RetomeAsync(
            new MemoryFilter { Tome = "alpha" },
            targetTome,
            CancellationToken.None,
            TestMemories.Agent));

        _store.UpdateCount.ShouldBe(0);
        _store.Records.ShouldAllBe(record => record.Tome == "alpha");
    }

    [Fact]
    public async Task Retome_ByFilter_SkipsTombstonedRecordsOfSourceTome()
    {
        var active = Seed(tome: "alpha");
        var buried = Seed(
            tome: "alpha",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "wrong info",
            tombstonedAt: FakeClock.DefaultStart);

        var result = await _service.RetomeAsync(
            new MemoryFilter { Tome = "alpha" },
            "beta",
            CancellationToken.None,
            TestMemories.Agent);

        result.Retomed.ShouldBe(1);
        var storedBuried = (await _store.GetAsync(buried.Id, CancellationToken.None))!;
        storedBuried.Tome.ShouldBe("alpha");
        storedBuried.Status.ShouldBe(MemoryStatus.Tombstoned);
        storedBuried.TombstoneReason.ShouldBe("wrong info");
        storedBuried.Corrections.ShouldBe([]);
        _store.Records.Single(record => record.Id == active.Id).Tome.ShouldBe("beta");
    }

    [Fact]
    public async Task Retome_ByIdList_TombstonedRecordIsNotRewritten()
    {
        var active = Seed(tome: "alpha");
        var buried = Seed(
            tome: "alpha",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "gone",
            tombstonedAt: FakeClock.DefaultStart);

        var result = await _service.RetomeAsync(
            [active.Id, buried.Id],
            "beta",
            CancellationToken.None,
            TestMemories.Agent);

        result.Retomed.ShouldBe(1);
        _store.Records.Single(record => record.Id == buried.Id).Tome.ShouldBe("alpha");
        _store.Records.Single(record => record.Id == active.Id).Tome.ShouldBe("beta");
    }

    private MemoryRecord Seed(
        string? tome = null,
        MemoryStatus status = MemoryStatus.Active,
        string? tombstoneReason = null,
        DateTimeOffset? tombstonedAt = null,
        IReadOnlyList<CorrectionEntry>? corrections = null)
    {
        var record = TestMemories.Create(
            "deploy on friday",
            [1f, 0f, 0f],
            tome: tome,
            status: status,
            tombstoneReason: tombstoneReason,
            tombstonedAt: tombstonedAt,
            corrections: corrections);
        _store.Seed(record, [1f, 0f, 0f]);
        return record;
    }
}
