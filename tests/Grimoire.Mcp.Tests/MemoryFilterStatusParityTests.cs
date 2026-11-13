using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Mcp.Tests;

public sealed class MemoryFilterStatusParityTests
{
    private static readonly DateTimeOffset Moment = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static readonly float[] Vector = [1f, 0f];

    private readonly FakeMemoryStore _store = new();

    [Fact]
    public async Task List_WithNoStatusClause_ReturnsActiveOnly()
    {
        await SeedSpreadAsync();

        var records = await _store.ListAsync(new MemoryFilter(), CancellationToken.None);

        records.Select(record => record.Status).ShouldBe([MemoryStatus.Active], ignoreOrder: true);
    }

    [Fact]
    public async Task List_WithBothStatusClauses_StatusListWins()
    {
        await SeedSpreadAsync();

        var records = await _store.ListAsync(
            new MemoryFilter { Status = MemoryStatus.Superseded, Statuses = [MemoryStatus.Active, MemoryStatus.Tombstoned] },
            CancellationToken.None);

        records.Select(record => record.Status)
            .ShouldBe([MemoryStatus.Active, MemoryStatus.Tombstoned], ignoreOrder: true);
    }

    // Sequential on purpose: the fake's UpsertAsync yields before writing into a plain
    // Dictionary, so seeding several records in parallel races the collection.
    private async Task SeedSpreadAsync()
    {
        await SeedAsync("active", MemoryStatus.Active);
        await SeedAsync("superseded", MemoryStatus.Superseded);
        await SeedAsync("tombstoned", MemoryStatus.Tombstoned);
    }

    private Task SeedAsync(string content, MemoryStatus status)
    {
        return _store.UpsertAsync(
            new MemoryRecord
            {
                Id = Guid.NewGuid(),
                Content = content,
                Scope = MemoryScope.Project,
                Kind = MemoryKind.Journal,
                Actor = MemoryActor.Human,
                CreatedAt = Moment,
                LastAccessedAt = Moment,
                Status = status,
                SupersededBy = status == MemoryStatus.Superseded ? Guid.NewGuid() : null,
                TombstoneReason = status == MemoryStatus.Tombstoned ? "wrong" : null,
            },
            Vector,
            CancellationToken.None);
    }
}
