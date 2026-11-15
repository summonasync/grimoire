using Grimoire.Core;
using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryFilterActorParityTests
{
    private static readonly MemoryActor Mine = MemoryActor.Agent("kelthuzad", "qwen");
    private static readonly MemoryActor Other = MemoryActor.Agent("thrall", "claude");

    private readonly FakeMemoryStore _store = new();

    private readonly MemoryRecord _mineRow;

    public MemoryFilterActorParityTests()
    {
        _mineRow = Seed("mine", Mine);
        Seed("other", Other);
        Seed("human", MemoryActor.Human);
    }

    [Fact]
    public async Task List_WithAnAlias_KeepsOnlyThatActor()
    {
        var records = await _store.ListAsync(new MemoryFilter { Actor = "kelthuzad" }, CancellationToken.None);

        records.Select(record => record.Content).ShouldBe(["mine"]);
    }

    [Fact]
    public async Task List_WithADifferentCaseAlias_ReturnsNothing()
    {
        // Same rule the store applies; the matcher beside it would keep the row. Pinned, not aligned.
        var records = await _store.ListAsync(new MemoryFilter { Actor = "Kelthuzad" }, CancellationToken.None);

        records.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_WithAWrongVendor_ReturnsNothing()
    {
        var records = await _store.ListAsync(new MemoryFilter { Actor = "kelthuzad+claude" }, CancellationToken.None);

        records.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_WithATrailingSeparator_ReadsAsAliasOnly()
    {
        var records = await _store.ListAsync(new MemoryFilter { Actor = "kelthuzad+" }, CancellationToken.None);

        records.Select(record => record.Content).ShouldBe(["mine"]);
    }

    [Fact]
    public async Task TrailingSeparator_StoreClauseAndMatcherAcceptTheIdenticalRows()
    {
        // One candidate, two readers, one answer. The store side is this fake's clause (it filters
        // through MemoryActorSemantics.MatchesExactly, the same reading the Qdrant translator emits);
        // the matcher side is MemoryFilterExtensions.Matches, which used to reject every row a
        // trailing separator reached and left the two halves disagreeing on the recall path. Both
        // vendors of the alias are in the fixture because "any vendor" is only a statement if there
        // is more than one vendor to take.
        var otherVendor = Seed("mine-glm", MemoryActor.Agent("kelthuzad", "glm"));
        var filter = new MemoryFilter { Actor = "kelthuzad+" };
        Guid[] expected = [_mineRow.Id, otherVendor.Id];

        var listed = await _store.ListAsync(filter, CancellationToken.None);
        var matched = _store.Records.Where(record => filter.Matches(record)).ToArray();

        listed.Select(record => record.Id).ShouldBe(expected, ignoreOrder: true);
        matched.Select(record => record.Id).ShouldBe(expected, ignoreOrder: true);
    }

    private MemoryRecord Seed(string content, MemoryActor actor)
    {
        var record = TestMemories.Create(content, [1f, 0f], actor: actor);
        _store.Seed(record, [1f, 0f]);
        return record;
    }
}
