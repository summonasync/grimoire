using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class RecallEscalationLoopTests
{
    private static readonly float[] QueryVector = [1f, 0f, 0f];

    private static readonly DateTimeOffset Now = FakeClock.DefaultStart;

    private readonly FakeMemoryStore _store = new();

    private readonly RecallEscalationLoop _loop;

    public RecallEscalationLoopTests()
    {
        _loop = new RecallEscalationLoop(_store);
    }

    [Fact]
    public async Task Search_WindowFillsWithRejects_WidensUntilTheDeeperRowLands()
    {
        SeedRejects(3);
        var wanted = SeedWanted("deep match", [0.8f, 0.6f, 0f]);

        var result = await _loop.SearchAsync(QueryVector, Windowed(), limit: 1, CancellationToken.None);

        result.Rounds.ShouldBe(2);
        result.StoppedBy.ShouldBe(RecallStopReason.PageFilled);
        result.Kept.Select(match => match.Record.Id).ShouldBe([wanted.Id]);
        _store.SearchCallCount.ShouldBe(2);
    }

    [Fact]
    public async Task Search_StoreCameBackShort_StopsAfterOneRound()
    {
        SeedWanted("only 1", [0.9f, 0.4359f, 0f]);
        SeedWanted("only 2", [0.8f, 0.6f, 0f]);

        var result = await _loop.SearchAsync(QueryVector, Windowed(), limit: 5, CancellationToken.None);

        result.Rounds.ShouldBe(1);
        result.StoppedBy.ShouldBe(RecallStopReason.StoreExhausted);
        result.Kept.Count.ShouldBe(2);
        _store.SearchCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Search_EveryRoundFillsAndNothingSurvives_StopsAtTheCap()
    {
        SeedRejects(24);

        var result = await _loop.SearchAsync(QueryVector, Windowed(), limit: 1, CancellationToken.None);

        result.Rounds.ShouldBe(RecallEscalationLoop.MaxRounds);
        _store.SearchCallCount.ShouldBe(4);
        result.StoppedBy.ShouldBe(RecallStopReason.RoundCapReached);
        result.Kept.ShouldBeEmpty();
    }

    [Fact]
    public async Task Search_WiderWindowDropsARow_RoundsUnionInsteadOfReplace()
    {
        SeedRejects(5);
        var vanishing = SeedWanted("first round only", [0.8f, 0.6f, 0f]);
        var loop = new RecallEscalationLoop(
            new ApproximateWindowStore(_store, vanishing.Id, firstWindow: 6));

        var result = await loop.SearchAsync(QueryVector, Windowed(), limit: 2, CancellationToken.None);

        result.Rounds.ShouldBe(2);
        result.StoppedBy.ShouldBe(RecallStopReason.StoreExhausted);
        result.Kept.Select(match => match.Record.Id).ShouldBe([vanishing.Id]);
    }

    [Fact]
    public async Task Search_RowSeenInTwoRounds_IsKeptOnce()
    {
        SeedRejects(5);
        var seen = SeedWanted("seen twice", [0.8f, 0.6f, 0f]);

        var result = await _loop.SearchAsync(QueryVector, Windowed(), limit: 2, CancellationToken.None);

        result.Rounds.ShouldBe(2);
        result.Kept.Select(match => match.Record.Id).ShouldBe([seen.Id]);
    }

    [Fact]
    public async Task Search_OversamplingClampedToOne_FirstRoundStillAsksForARealWindow()
    {
        SeedWanted("only row", [0.9f, 0.4359f, 0f]);
        var loop = new RecallEscalationLoop(_store, new MemoryEngineOptions { RecallOversampling = 0 });

        var result = await loop.SearchAsync(QueryVector, Windowed(), limit: 3, CancellationToken.None);

        // Without the clamp the first window is zero points wide, every round comes back
        // "full-sized" at zero rows, and the loop only ends by burning all four rounds.
        result.Rounds.ShouldBe(1);
        result.StoppedBy.ShouldBe(RecallStopReason.StoreExhausted);
        result.Kept.Count.ShouldBe(1);
        _store.SearchCallCount.ShouldBe(1);
    }

    private static MemoryFilter Windowed() => new()
    {
        Scopes = MemoryScopes.LongTerm,
        Status = MemoryStatus.Active,
        CreatedBefore = Now,
    };

    private void SeedRejects(int count)
    {
        for (var index = 1; index <= count; index++)
        {
            var vector = new[] { 0.99f - index * 0.001f, 0.141f, 0f };
            _store.Seed(TestMemories.Create($"noise {index}", vector, created: Now.AddDays(1)), vector);
        }
    }

    private MemoryRecord SeedWanted(string content, float[] vector)
    {
        var record = TestMemories.Create(content, vector, created: Now.AddDays(-1));
        _store.Seed(record, vector);
        return record;
    }
}
