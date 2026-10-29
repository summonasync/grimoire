using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class KnownTomeKeysTests
{
    private readonly FakeMemoryStore _store = new();

    [Fact]
    public async Task ListAsync_OnAnEmptyStore_ReturnsNothing()
    {
        var keys = await KnownTomeKeys.ListAsync(_store, CancellationToken.None);

        keys.ShouldBe([]);
    }

    [Fact]
    public async Task ListAsync_UnionsActiveAndTombstonedTomes_DistinctAndOrdinal()
    {
        _store.Seed(TestMemories.Create("alpha fact", [1f, 0f, 0f], tome: "zeta"));
        _store.Seed(TestMemories.Create("alpha note", [0f, 1f, 0f], tome: "alpha"));
        _store.Seed(TestMemories.Create("buried note", [0f, 0f, 1f], tome: "beta", status: MemoryStatus.Tombstoned));
        _store.Seed(TestMemories.Create("second alpha note", [1f, 1f, 0f], tome: "alpha"));

        var keys = await KnownTomeKeys.ListAsync(_store, CancellationToken.None);

        keys.ShouldBe(["alpha", "beta", "zeta"]);
    }

    [Fact]
    public async Task ListAsync_SkipsSupersededOnlyTomes()
    {
        _store.Seed(TestMemories.Create(
            "old truth", [1f, 0f, 0f], status: MemoryStatus.Superseded, supersededBy: Guid.NewGuid()));

        var keys = await KnownTomeKeys.ListAsync(_store, CancellationToken.None);

        keys.ShouldBe([]);
    }

    [Fact]
    public async Task EmptyTomeWarning_NoExplicitTome_IsSkippedWithoutTouchingTheStore()
    {
        var warnings = await EmptyTomeWarning.BuildAsync(
            _store, isEmpty: true, requestedTome: null, resolvedTome: "default", CancellationToken.None);

        warnings.ShouldBeNull();
        _store.ListCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task EmptyTomeWarning_UnknownTomeOnEmptyStore_NamesTheKnownKeys()
    {
        _store.Seed(TestMemories.Create("inno note", [1f, 0f, 0f], tome: "inno-crew"));

        var warnings = await EmptyTomeWarning.BuildAsync(
            _store, isEmpty: true, requestedTome: "linnovate", resolvedTome: "linnovate", CancellationToken.None);

        warnings.ShouldBe(["tome 'linnovate' is empty or does not exist. Known tome keys: inno-crew"]);
    }

    [Fact]
    public async Task EmptyTomeWarning_EmptyStore_UsesTheNoneYetWording()
    {
        var warnings = await EmptyTomeWarning.BuildAsync(
            _store, isEmpty: true, requestedTome: "linnovate", resolvedTome: "linnovate", CancellationToken.None);

        warnings.ShouldBe(["tome 'linnovate' is empty or does not exist. Known tome keys: (none yet)"]);
    }

    [Fact]
    public async Task EmptyTomeWarning_KnownButEmptyTome_StaysSilent()
    {
        _store.Seed(TestMemories.Create(
            "buried note", [1f, 0f, 0f], tome: "inno-crew", status: MemoryStatus.Tombstoned));

        var warnings = await EmptyTomeWarning.BuildAsync(
            _store, isEmpty: true, requestedTome: "inno-crew", resolvedTome: "inno-crew", CancellationToken.None);

        warnings.ShouldBeNull();
    }

    [Fact]
    public async Task EmptyTomeWarning_WithHits_IsSkippedWithoutTouchingTheStore()
    {
        _store.Seed(TestMemories.Create("inno note", [1f, 0f, 0f], tome: "inno-crew"));

        var warnings = await EmptyTomeWarning.BuildAsync(
            _store, isEmpty: false, requestedTome: "linnovate", resolvedTome: "linnovate", CancellationToken.None);

        warnings.ShouldBeNull();
        _store.ListCallCount.ShouldBe(0);
    }
}
