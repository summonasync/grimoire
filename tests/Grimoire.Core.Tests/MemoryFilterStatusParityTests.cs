using Grimoire.Core;
using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryFilterStatusParityTests
{
    private readonly FakeMemoryStore _store = new();

    public MemoryFilterStatusParityTests()
    {
        Seed("active", MemoryStatus.Active);
        Seed("superseded", MemoryStatus.Superseded);
        Seed("tombstoned", MemoryStatus.Tombstoned);
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

    private void Seed(string content, MemoryStatus status)
    {
        var record = TestMemories.Create(content, [1f, 0f, 0f], status: status);
        _store.Seed(record, [1f, 0f, 0f]);
    }
}
