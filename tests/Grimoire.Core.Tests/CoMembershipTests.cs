using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class CoMembershipTests
{
    private static readonly MemoryActor FirstActor = MemoryActor.Agent("coder-a", "claude");

    private static readonly MemoryActor SecondActor = MemoryActor.Agent("coder-b", "codex");

    [Fact]
    public void Edges_SharedTag_PairsMembers()
    {
        var first = TestMemories.Create("first", [1f], tags: ["deploy"], actor: FirstActor);
        var second = TestMemories.Create("second", [1f], tags: ["deploy", "ops"], actor: SecondActor);

        var edge = CoMembership.Edges([first, second]).ShouldHaveSingleItem();

        edge.Class.ShouldBe(RecordEdgeClass.CoMembership);
        edge.TargetStatus.ShouldBe(MemoryStatus.Active);
        new[] { edge.FromId, edge.ToId }.ShouldBe(new[] { first.Id, second.Id }, ignoreOrder: true);
    }

    [Fact]
    public void Edges_SharedProjectAlias_PairsMembers()
    {
        var first = TestMemories.Create("first", [1f], projectId: "grimoire", tags: ["one"], actor: FirstActor);
        var second = TestMemories.Create("second", [1f], projectId: "grimoire", tags: ["two"], actor: SecondActor);

        CoMembership.Edges([first, second]).ShouldHaveSingleItem();
    }

    [Fact]
    public void Edges_SharedActor_PairsMembers()
    {
        var first = TestMemories.Create("first", [1f], projectId: "grimoire", tags: ["one"], actor: FirstActor);
        var second = TestMemories.Create("second", [1f], projectId: "squeezr", tags: ["two"], actor: FirstActor);

        CoMembership.Edges([first, second]).ShouldHaveSingleItem();
    }

    [Fact]
    public void Edges_NoSharedDimensions_YieldNoEdges()
    {
        var first = TestMemories.Create("first", [1f], projectId: "grimoire", tags: ["one"], actor: FirstActor);
        var second = TestMemories.Create("second", [1f], projectId: "squeezr", tags: ["two"], actor: SecondActor);

        CoMembership.Edges([first, second]).ShouldBeEmpty();
    }

    [Fact]
    public void Edges_SharedTagAcrossThreeRecords_YieldEveryPair()
    {
        var first = TestMemories.Create("first", [1f], tags: ["deploy"], actor: FirstActor);
        var second = TestMemories.Create("second", [1f], tags: ["deploy"], actor: MemoryActor.Agent("coder-b", "codex"));
        var third = TestMemories.Create("third", [1f], tags: ["deploy"], actor: MemoryActor.Agent("coder-c", "qwen"));

        var edges = CoMembership.Edges([first, second, third]);

        edges.Count.ShouldBe(3);
        edges.ShouldAllBe(edge => edge.Class == RecordEdgeClass.CoMembership);
    }

    [Fact]
    public void Edges_PairSharedOnSeveralDimensions_YieldSingleEdge()
    {
        var first = TestMemories.Create("first", [1f], projectId: "grimoire", tags: ["deploy"], actor: FirstActor);
        var second = TestMemories.Create("second", [1f], projectId: "grimoire", tags: ["deploy"], actor: FirstActor);

        CoMembership.Edges([first, second]).ShouldHaveSingleItem();
    }

    [Fact]
    public void Edges_SupersededAndTombstonedRecords_AreExcluded()
    {
        var active = TestMemories.Create("active", [1f], tags: ["deploy"], actor: FirstActor);
        var superseded = TestMemories.Create("old", [1f], tags: ["deploy"], actor: FirstActor,
            status: MemoryStatus.Superseded, supersededBy: active.Id);
        var tombstoned = TestMemories.Create("gone", [1f], tags: ["deploy"], actor: FirstActor,
            status: MemoryStatus.Tombstoned, tombstoneReason: "stale");

        CoMembership.Edges([active, superseded]).ShouldBeEmpty();
        CoMembership.Edges([active, tombstoned]).ShouldBeEmpty();
    }
}
