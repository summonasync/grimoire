using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryServiceForgetTests
{
    private readonly FakeMemoryStore _store = new();

    private readonly FakeClock _clock = new();

    private readonly MemoryService _service;

    public MemoryServiceForgetTests()
    {
        _service = TestMemories.Service(
            _store,
            new FakeEmbeddingProvider(new Dictionary<string, float[]>()),
            _clock);
    }

    [Fact]
    public async Task Forget_WritesTombstoneWithoutDeleting()
    {
        var record = Seed();
        _clock.Advance(TimeSpan.FromHours(1));

        var forgotten = await _service.ForgetAsync(record.Id, "superseded by decision ADR-009", CancellationToken.None);

        forgotten.ShouldBeTrue();
        var stored = (await _store.GetAsync(record.Id, CancellationToken.None))!;
        stored.Status.ShouldBe(MemoryStatus.Tombstoned);
        stored.TombstoneReason.ShouldBe("superseded by decision ADR-009");
        stored.TombstonedAt.ShouldBe(_clock.UtcNow);
        _store.Records.ShouldContain(storedRecord => storedRecord.Id == record.Id);
        _store.DeleteCount.ShouldBe(0);
    }

    [Fact]
    public async Task Forget_MissingRecord_ReturnsFalse()
    {
        var forgotten = await _service.ForgetAsync(Guid.NewGuid(), "gone", CancellationToken.None);

        forgotten.ShouldBeFalse();
    }

    [Fact]
    public async Task Forget_AlreadyTombstoned_ReturnsFalseAndKeepsOriginalReason()
    {
        var record = Seed(status: MemoryStatus.Tombstoned, tombstoneReason: "original reason", tombstonedAt: FakeClock.DefaultStart);
        _clock.Advance(TimeSpan.FromDays(2));

        var forgotten = await _service.ForgetAsync(record.Id, "second reason", CancellationToken.None);

        forgotten.ShouldBeFalse();
        var stored = (await _store.GetAsync(record.Id, CancellationToken.None))!;
        stored.TombstoneReason.ShouldBe("original reason");
        stored.TombstonedAt.ShouldBe(FakeClock.DefaultStart);
    }

    [Fact]
    public async Task Forget_SupersededRecord_StillTombstones()
    {
        var record = Seed(status: MemoryStatus.Superseded, supersededBy: Guid.NewGuid());

        var forgotten = await _service.ForgetAsync(record.Id, "cleanup", CancellationToken.None);

        forgotten.ShouldBeTrue();
        (await _store.GetAsync(record.Id, CancellationToken.None))!.Status.ShouldBe(MemoryStatus.Tombstoned);
    }

    [Fact]
    public async Task Forget_EmptyReason_Throws()
    {
        var record = Seed();

        await Should.ThrowAsync<ArgumentException>(() => _service.ForgetAsync(record.Id, " ", CancellationToken.None));
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
