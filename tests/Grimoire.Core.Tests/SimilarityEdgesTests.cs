using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class SimilarityEdgesTests
{
    [Fact]
    public void Edges_CosineAboveThreshold_PairsRecords()
    {
        var first = TestMemories.Point("first", Direction(0.83));
        var second = TestMemories.Point("second", Direction(1));

        var edge = SimilarityEdges.Edges([first, second], 0.82).ShouldHaveSingleItem();

        edge.Class.ShouldBe(RecordEdgeClass.Similarity);
        edge.TargetStatus.ShouldBe(MemoryStatus.Active);
        new[] { edge.FromId, edge.ToId }.ShouldBe(new[] { first.Record.Id, second.Record.Id }, ignoreOrder: true);
    }

    [Fact]
    public void Edges_CosineBelowThreshold_YieldsNoEdges()
    {
        var first = TestMemories.Point("first", Direction(0.81));
        var second = TestMemories.Point("second", Direction(1));

        SimilarityEdges.Edges([first, second], 0.82).ShouldBeEmpty();
    }

    [Fact]
    public void Edges_IdenticalVectors_YieldSingleNormalizedPair()
    {
        var first = TestMemories.Point("first", [1f, 0f]);
        var second = TestMemories.Point("second", [1f, 0f]);

        var edge = SimilarityEdges.Edges([first, second], 0.82).ShouldHaveSingleItem();

        edge.FromId.ShouldBeLessThan(edge.ToId);
    }

    [Fact]
    public void Edges_DuplicatedRecordId_YieldsNoSelfEdge()
    {
        var record = TestMemories.Create("record", [1f, 0f]);
        float[] vector = [1f, 0f];
        var points = new[]
        {
            new MemoryPoint { Record = record, Vector = vector },
            new MemoryPoint { Record = record, Vector = vector },
        };

        SimilarityEdges.Edges(points, 0.82).ShouldBeEmpty();
    }

    [Fact]
    public void Edges_VectorsAcrossTomes_YieldNoEdges()
    {
        var first = TestMemories.Point("first", [1f, 0f], tome: "alpha");
        var second = TestMemories.Point("second", [1f, 0f], tome: "beta");

        SimilarityEdges.Edges([first, second], 0.82).ShouldBeEmpty();
    }

    [Fact]
    public void Edges_SupersededAndTombstonedRecords_AreExcluded()
    {
        var active = TestMemories.Point("active", [1f, 0f]);
        var superseded = TestMemories.Point("old", [1f, 0f], status: MemoryStatus.Superseded);
        var tombstoned = TestMemories.Point("gone", [1f, 0f], status: MemoryStatus.Tombstoned);

        SimilarityEdges.Edges([active, superseded], 0.82).ShouldBeEmpty();
        SimilarityEdges.Edges([active, tombstoned], 0.82).ShouldBeEmpty();
    }

    [Fact]
    public void Edges_ZeroVector_YieldsNoEdges()
    {
        var first = TestMemories.Point("first", [0f, 0f]);
        var second = TestMemories.Point("second", [1f, 0f]);

        SimilarityEdges.Edges([first, second], 0.82).ShouldBeEmpty();
    }

    [Fact]
    public void Edges_EmptySet_YieldsNoEdges()
    {
        SimilarityEdges.Edges([], 0.82).ShouldBeEmpty();
    }

    private static float[] Direction(double cosine) =>
        [(float)cosine, (float)Math.Sqrt(1 - cosine * cosine)];
}
