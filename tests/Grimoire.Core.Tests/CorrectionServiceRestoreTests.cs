using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class CorrectionServiceRestoreTests
{
    private readonly FakeMemoryStore _store = new();

    private readonly FakeClock _clock = new();

    private readonly CorrectionService _service;

    public CorrectionServiceRestoreTests()
    {
        _service = TestMemories.Corrections(_store, _clock);
    }

    [Fact]
    public async Task Restore_SupersededRecord_ActivatesOriginalAndTombstonesSuccessor()
    {
        var successor = TestMemories.Create("deploy moved to monday", [1f, 0f, 0f]);
        var original = TestMemories.Create("deploy on friday", [1f, 0f, 0f], status: MemoryStatus.Superseded, supersededBy: successor.Id);
        _store.Seed(successor, [1f, 0f, 0f]);
        _store.Seed(original, [1f, 0f, 0f]);
        _clock.Advance(TimeSpan.FromHours(3));

        var result = await _service.RestoreAsync(original.Id, "friday was right", CancellationToken.None);

        result.Restored.ShouldBeTrue();
        result.Record!.Status.ShouldBe(MemoryStatus.Active);

        var restored = (await _store.GetAsync(original.Id, CancellationToken.None))!;
        restored.Status.ShouldBe(MemoryStatus.Active);
        restored.TombstoneReason.ShouldBeNull();
        restored.TombstonedAt.ShouldBeNull();
        restored.SupersededBy.ShouldBe(successor.Id);
        var restoreLine = restored.Corrections.Single();
        restoreLine.Action.ShouldBe(CorrectionAction.Restore);
        restoreLine.Reason.ShouldBe("friday was right");
        restoreLine.At.ShouldBe(_clock.UtcNow);

        var tombstoned = (await _store.GetAsync(successor.Id, CancellationToken.None))!;
        tombstoned.Status.ShouldBe(MemoryStatus.Tombstoned);
        tombstoned.TombstoneReason.ShouldBe($"restored {original.Id}");
        tombstoned.TombstonedAt.ShouldBe(_clock.UtcNow);
        var supersedeLine = tombstoned.Corrections.Single();
        supersedeLine.Action.ShouldBe(CorrectionAction.Supersede);
        supersedeLine.Reason.ShouldBe($"restored {original.Id}");

        _store.Records.Count(record => record.Status == MemoryStatus.Active).ShouldBe(1);
        _store.Records.Single(record => record.Status == MemoryStatus.Active).Id.ShouldBe(original.Id);
    }

    [Fact]
    public async Task Restore_AlreadyTombstonedSuperseder_KeepsItsOriginalReason()
    {
        var successor = TestMemories.Create(
            "deploy moved to monday",
            [1f, 0f, 0f],
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "dead on arrival",
            tombstonedAt: FakeClock.DefaultStart);
        var original = TestMemories.Create("deploy on friday", [1f, 0f, 0f], status: MemoryStatus.Superseded, supersededBy: successor.Id);
        _store.Seed(successor, [1f, 0f, 0f]);
        _store.Seed(original, [1f, 0f, 0f]);
        _clock.Advance(TimeSpan.FromHours(3));

        var result = await _service.RestoreAsync(original.Id, "friday was right", CancellationToken.None);

        result.Restored.ShouldBeTrue();
        (await _store.GetAsync(original.Id, CancellationToken.None))!.Status.ShouldBe(MemoryStatus.Active);

        var untouched = (await _store.GetAsync(successor.Id, CancellationToken.None))!;
        untouched.Status.ShouldBe(MemoryStatus.Tombstoned);
        untouched.TombstoneReason.ShouldBe("dead on arrival");
        untouched.TombstonedAt.ShouldBe(FakeClock.DefaultStart);
        untouched.Corrections.ShouldBe([]);
    }

    [Fact]
    public async Task Restore_TombstonedRecord_ClearsTombstoneFieldsAndAppendsTrail()
    {
        var record = Seed(status: MemoryStatus.Tombstoned, tombstoneReason: "bad call", tombstonedAt: FakeClock.DefaultStart);
        _clock.Advance(TimeSpan.FromDays(1));

        var result = await _service.RestoreAsync(record.Id, "the call was fine", CancellationToken.None);

        result.Restored.ShouldBeTrue();
        var restored = (await _store.GetAsync(record.Id, CancellationToken.None))!;
        restored.Status.ShouldBe(MemoryStatus.Active);
        restored.TombstoneReason.ShouldBeNull();
        restored.TombstonedAt.ShouldBeNull();
        var line = restored.Corrections.Single();
        line.Action.ShouldBe(CorrectionAction.Restore);
        line.Reason.ShouldBe("the call was fine");
        line.Actor.ShouldBe(MemoryActor.Human);
        line.At.ShouldBe(_clock.UtcNow);
    }

    [Fact]
    public async Task Restore_ActiveRecord_IsNoOpWithoutTrail()
    {
        var record = Seed();

        var result = await _service.RestoreAsync(record.Id, "why not", CancellationToken.None);

        result.Restored.ShouldBeFalse();
        result.Record!.Id.ShouldBe(record.Id);
        (await _store.GetAsync(record.Id, CancellationToken.None))!.Corrections.ShouldBe([]);
    }

    [Fact]
    public async Task Restore_MissingRecord_ReportsNotRestored()
    {
        var result = await _service.RestoreAsync(Guid.NewGuid(), "gone", CancellationToken.None);

        result.Restored.ShouldBeFalse();
        result.Record.ShouldBeNull();
    }

    [Fact]
    public async Task Restore_InOtherTome_IsInvisible()
    {
        var record = Seed(status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: FakeClock.DefaultStart);

        var result = await _service.RestoreAsync(record.Id, "undo", CancellationToken.None, tome: "elsewhere");

        result.Restored.ShouldBeFalse();
        result.Record.ShouldBeNull();
        (await _store.GetAsync(record.Id, CancellationToken.None))!.Status.ShouldBe(MemoryStatus.Tombstoned);
    }

    [Fact]
    public async Task Restore_WithExplicitActor_RecordsItOnTheTrail()
    {
        var record = Seed(status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: FakeClock.DefaultStart);

        await _service.RestoreAsync(record.Id, "undo", CancellationToken.None, actor: TestMemories.Agent);

        var restored = (await _store.GetAsync(record.Id, CancellationToken.None))!;
        restored.Corrections.Single().Actor.ShouldBe(TestMemories.Agent);
    }

    [Fact]
    public async Task Restore_EmptyReason_Throws()
    {
        var record = Seed();

        await Should.ThrowAsync<ArgumentException>(() => _service.RestoreAsync(record.Id, " ", CancellationToken.None));
    }

    private MemoryRecord Seed(
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
            tombstonedAt: tombstonedAt);
        _store.Seed(record, [1f, 0f, 0f]);
        return record;
    }
}
