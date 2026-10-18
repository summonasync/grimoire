using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class CorrectionServiceHistoryTests
{
    private readonly FakeMemoryStore _store = new();

    private readonly FakeClock _clock = new();

    private readonly CorrectionService _service;

    public CorrectionServiceHistoryTests()
    {
        _service = TestMemories.Corrections(_store, _clock);
    }

    [Fact]
    public async Task History_ThreeGenerationChain_WalksBothDirectionsOldestFirst()
    {
        var chain = SeedChain();

        var fromOldest = await _service.HistoryAsync(chain[0].Id, CancellationToken.None);
        var fromNewest = await _service.HistoryAsync(chain[2].Id, CancellationToken.None);
        var fromMiddle = await _service.HistoryAsync(chain[1].Id, CancellationToken.None);

        fromOldest.Found.ShouldBeTrue();
        fromOldest.Chain.Select(entry => entry.Id).ShouldBe(chain.Select(record => record.Id));
        fromNewest.Chain.Select(entry => entry.Id).ShouldBe(chain.Select(record => record.Id));
        fromMiddle.Chain.Select(entry => entry.Id).ShouldBe(chain.Select(record => record.Id));
    }

    [Fact]
    public async Task History_Entries_CarryChainMetadataAndPreview()
    {
        var chain = SeedChain();

        var history = await _service.HistoryAsync(chain[0].Id, CancellationToken.None);

        history.Chain.Select(entry => entry.Status)
            .ShouldBe([MemoryStatus.Superseded, MemoryStatus.Superseded, MemoryStatus.Active]);
        history.Chain.Select(entry => entry.CreatedAt).ShouldBeInOrder(SortDirection.Ascending);
        history.Chain.Select(entry => entry.Actor).ShouldBe(chain.Select(record => record.Actor));
        history.Chain.Select(entry => entry.Tome).ShouldBe([TomeKey.DefaultTome, TomeKey.DefaultTome, TomeKey.DefaultTome]);
        history.Chain.Last().BodyPreview.ShouldBe("deploy moved to monday");
    }

    [Fact]
    public async Task History_Entries_ShowTombstoneMetadata()
    {
        var forgotten = TestMemories.Create(
            "wrong fact",
            [1f, 0f, 0f],
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "bad call",
            tombstonedAt: FakeClock.DefaultStart.AddHours(5));
        _store.Seed(forgotten, [1f, 0f, 0f]);

        var history = await _service.HistoryAsync(forgotten.Id, CancellationToken.None);

        var entry = history.Chain.Single();
        entry.Status.ShouldBe(MemoryStatus.Tombstoned);
        entry.TombstoneReason.ShouldBe("bad call");
        entry.TombstonedAt.ShouldBe(FakeClock.DefaultStart.AddHours(5));
    }

    [Fact]
    public async Task History_BodyPreview_CapsFirstLineAt120Chars()
    {
        var record = TestMemories.Create(new string('x', 200) + "\nsecond line", [1f, 0f, 0f]);
        _store.Seed(record, [1f, 0f, 0f]);

        var history = await _service.HistoryAsync(record.Id, CancellationToken.None);

        var preview = history.Chain.Single().BodyPreview;
        preview.Length.ShouldBe(120);
        preview.ShouldNotContain("second line");
    }

    [Fact]
    public async Task History_UnknownId_ReportsNotFound()
    {
        var history = await _service.HistoryAsync(Guid.NewGuid(), CancellationToken.None);

        history.Found.ShouldBeFalse();
        history.Chain.ShouldBe([]);
    }

    [Fact]
    public async Task History_InOtherTome_IsInvisible()
    {
        var record = TestMemories.Create("deploy on friday", [1f, 0f, 0f], tome: "alpha");
        _store.Seed(record, [1f, 0f, 0f]);

        var history = await _service.HistoryAsync(record.Id, CancellationToken.None, tome: "beta");

        history.Found.ShouldBeFalse();
        history.Chain.ShouldBe([]);
    }

    [Fact]
    public async Task History_AfterRestore_StillWalksThroughTheTombstonedSuccessor()
    {
        var successor = TestMemories.Create("deploy moved to monday", [1f, 0f, 0f], created: FakeClock.DefaultStart.AddHours(1));
        var original = TestMemories.Create("deploy on friday", [1f, 0f, 0f], status: MemoryStatus.Superseded, supersededBy: successor.Id);
        _store.Seed(successor, [1f, 0f, 0f]);
        _store.Seed(original, [1f, 0f, 0f]);
        _clock.Advance(TimeSpan.FromHours(2));
        await _service.RestoreAsync(original.Id, "friday was right", CancellationToken.None);

        var history = await _service.HistoryAsync(successor.Id, CancellationToken.None);

        history.Chain.Select(entry => entry.Id).ShouldBe([original.Id, successor.Id]);
        history.Chain.Select(entry => entry.Status).ShouldBe([MemoryStatus.Active, MemoryStatus.Tombstoned]);
    }

    private MemoryRecord[] SeedChain()
    {
        var newest = TestMemories.Create("deploy moved to monday", [1f, 0f, 0f], created: FakeClock.DefaultStart.AddHours(2));
        var middle = TestMemories.Create(
            "deploy moved to sunday",
            [1f, 0f, 0f],
            created: FakeClock.DefaultStart.AddHours(1),
            status: MemoryStatus.Superseded,
            supersededBy: newest.Id);
        var oldest = TestMemories.Create(
            "deploy on friday",
            [1f, 0f, 0f],
            status: MemoryStatus.Superseded,
            supersededBy: middle.Id);

        foreach (var record in new[] { oldest, middle, newest })
        {
            _store.Seed(record, [1f, 0f, 0f]);
        }

        return [oldest, middle, newest];
    }
}
