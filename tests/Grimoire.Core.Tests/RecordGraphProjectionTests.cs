using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class RecordGraphProjectionTests
{
    [Fact]
    public void Project_SupersededByInSet_ProducesDirectionalEdge()
    {
        var successor = TestMemories.Create("successor", [1f]);
        var superseded = TestMemories.Create("superseded", [1f], supersededBy: successor.Id);

        var graph = RecordGraphProjection.Project([superseded, successor]);

        graph.Edges.ShouldBe([new RecordEdge
        {
            FromId = superseded.Id,
            ToId = successor.Id,
            Class = RecordEdgeClass.Supersede,
            TargetStatus = MemoryStatus.Active,
        }]);
    }

    [Fact]
    public void Project_SupersedeChain_ProducesDirectionalEdgesAlongIt()
    {
        var oldest = TestMemories.Create("oldest", [1f]);
        var middle = TestMemories.Create("middle", [1f], supersededBy: oldest.Id);
        var newest = TestMemories.Create("newest", [1f], supersededBy: middle.Id);

        var graph = RecordGraphProjection.Project([newest, middle, oldest]);

        graph.Edges.Count.ShouldBe(2);
        graph.Edges.ShouldContain(edge =>
            edge.FromId == middle.Id && edge.ToId == oldest.Id && edge.Class == RecordEdgeClass.Supersede);
        graph.Edges.ShouldContain(edge =>
            edge.FromId == newest.Id && edge.ToId == middle.Id && edge.Class == RecordEdgeClass.Supersede);
    }

    [Fact]
    public void Project_SupersededByMissingRecord_YieldsNoEdge()
    {
        var record = TestMemories.Create("record", [1f], supersededBy: Guid.NewGuid());

        RecordGraphProjection.Project([record]).Edges.ShouldBeEmpty();
    }

    [Fact]
    public void Project_SelfSupersede_YieldsNoEdge()
    {
        var record = TestMemories.Create("record", [1f]);

        RecordGraphProjection.Project([record with { SupersededBy = record.Id }]).Edges.ShouldBeEmpty();
    }

    [Fact]
    public void Project_ContentLinkToInSetRecord_ProducesLinkEdge()
    {
        var target = TestMemories.Create("target", [1f]);
        var source = TestMemories.Create($"see [[{target.Id}]]", [1f]);

        var graph = RecordGraphProjection.Project([source, target]);

        graph.Edges.ShouldBe([new RecordEdge
        {
            FromId = source.Id,
            ToId = target.Id,
            Class = RecordEdgeClass.Link,
            TargetStatus = MemoryStatus.Active,
        }]);
    }

    [Fact]
    public void Project_ContentLinkToMissingRecord_YieldsNoEdge()
    {
        var source = TestMemories.Create($"see [[{Guid.NewGuid()}]]", [1f]);

        RecordGraphProjection.Project([source]).Edges.ShouldBeEmpty();
    }

    [Fact]
    public void Project_SelfLink_YieldsNoEdge()
    {
        var record = TestMemories.Create("draft", [1f]);

        RecordGraphProjection.Project([record with { Content = $"see [[{record.Id}]]" }])
            .Edges.ShouldBeEmpty();
    }

    [Fact]
    public void Project_MalformedLink_YieldsNoEdge()
    {
        var source = TestMemories.Create("see [[not-a-guid]]", [1f]);

        RecordGraphProjection.Project([source]).Edges.ShouldBeEmpty();
    }

    [Fact]
    public void Project_LinkToTombstonedRecord_KeepsEdgeAndCarriesStatus()
    {
        var target = TestMemories.Create("gone", [1f], status: MemoryStatus.Tombstoned, tombstoneReason: "stale");
        var source = TestMemories.Create($"see [[{target.Id}]]", [1f]);

        var edge = RecordGraphProjection.Project([source, target]).Edges.ShouldHaveSingleItem();

        edge.FromId.ShouldBe(source.Id);
        edge.ToId.ShouldBe(target.Id);
        edge.Class.ShouldBe(RecordEdgeClass.Link);
        edge.TargetStatus.ShouldBe(MemoryStatus.Tombstoned);
    }

    [Fact]
    public void Project_TombstoneReasonLink_ProducesLinkEdge()
    {
        var restored = TestMemories.Create("restored record", [1f]);
        var successor = TestMemories.Create("successor", [1f],
            status: MemoryStatus.Tombstoned,
            tombstoneReason: $"restored {restored.Id}");
        var restoredAgain = restored with { SupersededBy = successor.Id };

        var graph = RecordGraphProjection.Project([restoredAgain, successor]);

        graph.Edges.ShouldBe([
            new RecordEdge
            {
                FromId = restoredAgain.Id,
                ToId = successor.Id,
                Class = RecordEdgeClass.Supersede,
                TargetStatus = MemoryStatus.Tombstoned,
            },
            new RecordEdge
            {
                FromId = successor.Id,
                ToId = restoredAgain.Id,
                Class = RecordEdgeClass.Link,
                TargetStatus = MemoryStatus.Active,
            },
        ]);
    }

    [Fact]
    public void Project_RepeatedLink_YieldsSingleEdge()
    {
        var target = TestMemories.Create("target", [1f]);
        var source = TestMemories.Create($"[[{target.Id}]] and again [[{target.Id}]]", [1f]);

        RecordGraphProjection.Project([source, target]).Edges.ShouldHaveSingleItem();
    }

    [Fact]
    public void Project_EmptySet_YieldsEmptyGraph()
    {
        RecordGraphProjection.Project([]).Edges.ShouldBeEmpty();
    }

    [Fact]
    public void Project_DefaultOptions_LeavesCoMembershipOut()
    {
        var first = TestMemories.Create("first", [1f], tags: ["deploy"], actor: MemoryActor.Agent("coder-a", "claude"));
        var second = TestMemories.Create("second", [1f], tags: ["deploy"], actor: MemoryActor.Agent("coder-b", "codex"));

        RecordGraphProjection.Project([first, second]).Edges.ShouldBeEmpty();
    }

    [Fact]
    public void Project_WithCoMembership_IncludesSharedTagPair()
    {
        var first = TestMemories.Create("first", [1f], tags: ["deploy"], actor: MemoryActor.Agent("coder-a", "claude"));
        var second = TestMemories.Create("second", [1f], tags: ["deploy"], actor: MemoryActor.Agent("coder-b", "codex"));

        var graph = RecordGraphProjection.Project(
            [first, second],
            new RecordGraphOptions { IncludeCoMembership = true });

        var edge = graph.Edges.ShouldHaveSingleItem();

        edge.Class.ShouldBe(RecordEdgeClass.CoMembership);
        edge.TargetStatus.ShouldBe(MemoryStatus.Active);
        new[] { edge.FromId, edge.ToId }.ShouldBe(new[] { first.Id, second.Id }, ignoreOrder: true);
    }

    [Fact]
    public void Project_CoMembershipPairSharedOnSeveralDimensions_YieldsSingleEdge()
    {
        var actor = MemoryActor.Agent("coder-a", "claude");
        var first = TestMemories.Create("first", [1f], projectId: "grimoire", tags: ["deploy"], actor: actor);
        var second = TestMemories.Create("second", [1f], projectId: "grimoire", tags: ["deploy"], actor: actor);

        var graph = RecordGraphProjection.Project(
            [first, second],
            new RecordGraphOptions { IncludeCoMembership = true });

        graph.Edges.ShouldHaveSingleItem();
    }

    [Fact]
    public void Project_CoMembership_SkipsInactiveRecords()
    {
        var actor = MemoryActor.Agent("coder-a", "claude");
        var active = TestMemories.Create("active", [1f], tags: ["deploy"], actor: actor);
        var tombstoned = TestMemories.Create("gone", [1f], tags: ["deploy"], actor: actor,
            status: MemoryStatus.Tombstoned, tombstoneReason: "stale");

        var graph = RecordGraphProjection.Project(
            [active, tombstoned],
            new RecordGraphOptions { IncludeCoMembership = true });

        graph.Edges.ShouldBeEmpty();
    }

    [Fact]
    public void Project_TomeScopedSet_YieldsNoEdgesOutsideIt()
    {
        var foreign = TestMemories.Create("beta record", [1f], tome: "beta");
        var local = TestMemories.Create($"links [[{foreign.Id}]]", [1f], tome: "alpha", supersededBy: foreign.Id);

        RecordGraphProjection.Project([local]).Edges.ShouldBeEmpty();
    }

    [Fact]
    public void Project_EdgesOnlyReferenceGivenRecords()
    {
        var foreign = TestMemories.Create("beta record", [1f], tome: "beta");
        var local = TestMemories.Create($"links [[{foreign.Id}]]", [1f], tome: "alpha", supersededBy: foreign.Id);

        var graph = RecordGraphProjection.Project([local, foreign]);

        graph.Edges.ShouldAllBe(edge => edge.FromId == local.Id && edge.ToId == foreign.Id);
    }
}
