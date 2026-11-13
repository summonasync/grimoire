using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public sealed class MemoryFilterStatusParityTests
{
    private readonly FakeMemoryStore _store = new();

    public MemoryFilterStatusParityTests()
    {
        _store.Seed("active", [1f, 0f]);
        _store.Seed("superseded", [1f, 0f], status: MemoryStatus.Superseded, supersededBy: Guid.NewGuid());
        _store.Seed("tombstoned", [1f, 0f], status: MemoryStatus.Tombstoned, tombstoneReason: "wrong");
    }

    [Fact]
    public async Task List_WithNoStatusClause_ReturnsActiveOnly()
    {
        var records = await _store.ListAsync(new MemoryFilter(), CancellationToken.None);

        records.Select(record => record.Status).ShouldBe([MemoryStatus.Active], ignoreOrder: true);
    }

    [Fact]
    public async Task List_WithBothStatusClauses_StatusListWins()
    {
        var records = await _store.ListAsync(
            new MemoryFilter { Status = MemoryStatus.Superseded, Statuses = [MemoryStatus.Active, MemoryStatus.Tombstoned] },
            CancellationToken.None);

        records.Select(record => record.Status)
            .ShouldBe([MemoryStatus.Active, MemoryStatus.Tombstoned], ignoreOrder: true);
    }
}
