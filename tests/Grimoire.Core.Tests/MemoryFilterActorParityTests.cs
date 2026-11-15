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

    [Theory]
    [InlineData("+", null)]
    [InlineData("+qwen", "qwen")]
    public async Task BareSeparatorCandidate_NetsNoRowsOnBothSidesForAReasonNoRuleStates(string candidate, string? vendor)
    {
        // 058 WARNING-2, purchased into a named case by 061. The CLI write door refuses these
        // candidates (ActorResolverTests pins UsageException for "+" and "+claude"); the leg nobody
        // pinned is the free-text one — an actor arriving as an MCP argument or an Api query
        // parameter. Both readers net zero rows here, and neither one REJECTS the candidate: nothing
        // in the filter path calls a leading separator illegal. MemoryActor.AliasPart hands back the
        // empty string rather than null, so Segments reports a segment whose alias is empty and both
        // sides go looking for a record whose alias is "" — a row the write path cannot produce. That
        // is an agreement carried by the data, not by a rule, and it is the soft spot: teach Segments
        // that a blank alias segment means unconstrained — the reading a blank candidate already gets
        // — and both sides flip to every row at once. The case asymmetry that survived T4 is a
        // different candidate and 058's pinned policy, not this input.
        var filter = new MemoryFilter { Actor = candidate };

        var segment = MemoryActorSemantics.Segments(candidate).ShouldHaveSingleItem();
        segment.Alias.ShouldBeEmpty();
        segment.Vendor.ShouldBe(vendor);

        MemoryActorSemantics.MatchesExactly(Mine, candidate).ShouldBeFalse();
        filter.Matches(_mineRow).ShouldBeFalse();

        var listed = await _store.ListAsync(filter, CancellationToken.None);
        var matched = _store.Records.Where(record => filter.Matches(record)).ToArray();

        listed.ShouldBeEmpty();
        matched.ShouldBeEmpty();
        (await _store.ListAsync(new MemoryFilter(), CancellationToken.None)).ShouldNotBeEmpty();
    }

    private MemoryRecord Seed(string content, MemoryActor actor)
    {
        var record = TestMemories.Create(content, [1f, 0f], actor: actor);
        _store.Seed(record, [1f, 0f]);
        return record;
    }
}
