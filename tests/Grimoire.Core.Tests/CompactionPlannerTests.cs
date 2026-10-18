using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class CompactionPlannerTests
{
    private static readonly CompactionOptions Options = new();

    private static MemoryPoint Point(string content, float[] vector, DateTimeOffset accessed) => new()
    {
        Record = TestMemories.Create(content, vector, accessed: accessed),
        Vector = vector,
    };

    [Fact]
    public void Plan_SimilarStaleJournal_FormsOneCluster()
    {
        var stale = FakeClock.DefaultStart.AddDays(-30);
        var journal = new[]
        {
            Point("retry loop flaky", [1f, 0f, 0f], stale),
            Point("flaky retry loop", [0.97f, 0.24f, 0f], stale.AddMinutes(1)),
            Point("retry loop is flaky", [0.94f, 0.34f, 0f], stale.AddMinutes(2)),
            Point("unrelated note", [0f, 1f, 0f], stale),
        };

        var groups = CompactionPlanner.Plan(journal, FakeClock.DefaultStart, Options);

        groups.Count.ShouldBe(1);
        groups[0].MemoryIds.Count.ShouldBe(3);
        groups[0].Similarity.ShouldBeGreaterThanOrEqualTo(Options.MinSimilarity);
        groups[0].StalestAccess.ShouldBe(stale);
    }

    [Fact]
    public void Plan_RecentlyAccessedJournal_IsNotCandidate()
    {
        var fresh = FakeClock.DefaultStart.AddDays(-2);
        var journal = new[]
        {
            Point("retry loop flaky", [1f, 0f, 0f], fresh),
            Point("flaky retry loop", [0.97f, 0.24f, 0f], fresh),
        };

        var groups = CompactionPlanner.Plan(journal, FakeClock.DefaultStart, Options);

        groups.ShouldBeEmpty();
    }

    [Fact]
    public void Plan_DissimilarStaleJournal_FormsNoCluster()
    {
        var stale = FakeClock.DefaultStart.AddDays(-30);
        var journal = new[]
        {
            Point("build times", [1f, 0f, 0f], stale),
            Point("coffee tastes", [0f, 1f, 0f], stale),
        };

        var groups = CompactionPlanner.Plan(journal, FakeClock.DefaultStart, Options);

        groups.ShouldBeEmpty();
    }

    [Fact]
    public void Plan_ClustersBelowMinSize_AreNotReported()
    {
        var options = new CompactionOptions { MinClusterSize = 3 };
        var stale = FakeClock.DefaultStart.AddDays(-30);
        var journal = new[]
        {
            Point("retry loop flaky", [1f, 0f, 0f], stale),
            Point("flaky retry loop", [0.97f, 0.24f, 0f], stale.AddMinutes(1)),
        };

        var groups = CompactionPlanner.Plan(journal, FakeClock.DefaultStart, options);

        groups.ShouldBeEmpty();
    }

    [Fact]
    public void Plan_DissolvedCluster_FreesPointsForLaterLeaders()
    {
        var options = new CompactionOptions { MinClusterSize = 3, MinSimilarity = 0.85 };
        var baseTime = FakeClock.DefaultStart.AddDays(-30);
        var journal = new[]
        {
            Point("a", [1f, 0f, 0f], baseTime),
            Point("x", [0.93f, 0.37f, 0f], baseTime.AddMinutes(1)),
            Point("s", [0.8f, 0.6f, 0f], baseTime.AddMinutes(2)),
            Point("t", [0.78f, 0.63f, 0f], baseTime.AddMinutes(3)),
        };

        var groups = CompactionPlanner.Plan(journal, FakeClock.DefaultStart, options);

        groups.Count.ShouldBe(1);
        groups[0].MemoryIds.Count.ShouldBe(3);
        groups[0].StalestAccess.ShouldBe(baseTime.AddMinutes(1));
    }

    [Fact]
    public void Plan_GroupsAreOrderedStalestFirst()
    {
        var older = FakeClock.DefaultStart.AddDays(-60);
        var newer = FakeClock.DefaultStart.AddDays(-20);
        var journal = new[]
        {
            Point("new cluster 1", [1f, 0f, 0f], newer),
            Point("new cluster 2", [0.97f, 0.24f, 0f], newer.AddMinutes(1)),
            Point("old cluster 1", [0f, 1f, 0f], older),
            Point("old cluster 2", [0.24f, 0.97f, 0f], older.AddMinutes(1)),
        };

        var groups = CompactionPlanner.Plan(journal, FakeClock.DefaultStart, Options);

        groups.Count.ShouldBe(2);
        groups[0].StalestAccess.ShouldBeLessThan(groups[1].StalestAccess);
    }

    [Fact]
    public void Plan_IsDeterministic()
    {
        var stale = FakeClock.DefaultStart.AddDays(-30);
        var journal = new[]
        {
            Point("retry loop flaky", [1f, 0f, 0f], stale),
            Point("flaky retry loop", [0.97f, 0.24f, 0f], stale.AddMinutes(1)),
            Point("retry loop is flaky", [0.94f, 0.34f, 0f], stale.AddMinutes(2)),
        };

        var first = CompactionPlanner.Plan(journal, FakeClock.DefaultStart, Options);
        var second = CompactionPlanner.Plan(journal, FakeClock.DefaultStart, Options);

        first.ShouldBeEquivalentTo(second);
    }
}
