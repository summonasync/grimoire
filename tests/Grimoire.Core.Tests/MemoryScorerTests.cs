using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class MemoryScorerTests
{
    private static readonly RecencyHalfLives HalfLives = new();

    [Fact]
    public void Score_FreshRecord_IsSimilarityTimesImportanceWeight()
    {
        var record = TestMemories.Create("fresh", [1f, 0f], accessed: FakeClock.DefaultStart, importance: 1);

        var score = MemoryScorer.Score(0.8, record, FakeClock.DefaultStart, HalfLives);

        score.ShouldBe(0.8, 1e-9);
    }

    [Fact]
    public void Score_NeutralImportance_HalvesTheScore()
    {
        var record = TestMemories.Create("neutral", [1f, 0f], accessed: FakeClock.DefaultStart, importance: 0);

        var score = MemoryScorer.Score(0.8, record, FakeClock.DefaultStart, HalfLives);

        score.ShouldBe(0.4, 1e-9);
    }

    [Fact]
    public void Score_AfterOneHalfLife_HalvesTheScore()
    {
        var record = TestMemories.Create("aged", [1f, 0f], scope: MemoryScope.Project, accessed: FakeClock.DefaultStart, importance: 1);
        var now = FakeClock.DefaultStart.AddDays(30);

        var score = MemoryScorer.Score(0.8, record, now, HalfLives);

        score.ShouldBe(0.4, 1e-9);
    }

    [Fact]
    public void Score_SessionDecay_DropsFasterThanGlobal()
    {
        var session = TestMemories.Create("s", [1f, 0f], scope: MemoryScope.Session, accessed: FakeClock.DefaultStart);
        var global = TestMemories.Create("g", [1f, 0f], scope: MemoryScope.Global, accessed: FakeClock.DefaultStart);
        var now = FakeClock.DefaultStart.AddDays(5);

        var sessionScore = MemoryScorer.Score(1, session, now, HalfLives);
        var globalScore = MemoryScorer.Score(1, global, now, HalfLives);

        sessionScore.ShouldBeLessThan(globalScore);
        sessionScore.ShouldBe(0.75 * Math.Pow(0.5, 10), 1e-9);
    }

    [Fact]
    public void Score_ImportanceOutOfRange_IsClamped()
    {
        var tooHigh = TestMemories.Create("high", [1f, 0f], accessed: FakeClock.DefaultStart, importance: 7);
        var tooLow = TestMemories.Create("low", [1f, 0f], accessed: FakeClock.DefaultStart, importance: -3);

        var highScore = MemoryScorer.Score(0.8, tooHigh, FakeClock.DefaultStart, HalfLives);
        var lowScore = MemoryScorer.Score(0.8, tooLow, FakeClock.DefaultStart, HalfLives);

        highScore.ShouldBe(0.8, 1e-9);
        lowScore.ShouldBe(0.4, 1e-9);
    }

    [Fact]
    public void Score_FutureLastAccessedAt_DoesNotExceedSimilarity()
    {
        var record = TestMemories.Create("future", [1f, 0f], accessed: FakeClock.DefaultStart.AddDays(1), importance: 1);

        var score = MemoryScorer.Score(0.7, record, FakeClock.DefaultStart, HalfLives);

        score.ShouldBe(0.7, 1e-9);
    }

    [Fact]
    public void Score_ForeignProjectRecord_IsDemotedByConfiguredFactor()
    {
        var record = TestMemories.Create("foreign", [1f, 0f], accessed: FakeClock.DefaultStart, importance: 1, projectId: "beta");

        var score = MemoryScorer.Score(0.8, record, FakeClock.DefaultStart, HalfLives, callerProjectId: "alpha", foreignProjectFactor: 0.6);

        score.ShouldBe(0.48, 1e-9);
    }

    [Fact]
    public void Score_HomeProjectRecord_KeepsFullScore()
    {
        var record = TestMemories.Create("home", [1f, 0f], accessed: FakeClock.DefaultStart, importance: 1, projectId: "alpha");

        var score = MemoryScorer.Score(0.8, record, FakeClock.DefaultStart, HalfLives, callerProjectId: "alpha", foreignProjectFactor: 0.6);

        score.ShouldBe(0.8, 1e-9);
    }

    [Fact]
    public void Score_CrossProjectRecord_KeepsFullScore()
    {
        var record = TestMemories.Create("cross-project", [1f, 0f], scope: MemoryScope.Global, accessed: FakeClock.DefaultStart, importance: 1);

        var score = MemoryScorer.Score(0.8, record, FakeClock.DefaultStart, HalfLives, callerProjectId: "alpha", foreignProjectFactor: 0.6);

        score.ShouldBe(0.8, 1e-9);
    }

    [Fact]
    public void Score_UnresolvableCallerProject_NoDemotion()
    {
        var record = TestMemories.Create("foreign", [1f, 0f], accessed: FakeClock.DefaultStart, importance: 1, projectId: "beta");

        var score = MemoryScorer.Score(0.8, record, FakeClock.DefaultStart, HalfLives, callerProjectId: null, foreignProjectFactor: 0.6);

        score.ShouldBe(0.8, 1e-9);
    }
}
