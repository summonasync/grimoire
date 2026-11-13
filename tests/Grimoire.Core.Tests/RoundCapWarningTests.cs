using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class RoundCapWarningTests
{
    [Theory]
    [InlineData(RecallStopReason.PageFilled)]
    [InlineData(RecallStopReason.StoreExhausted)]
    public void Build_AStopThatIsNotTheCap_StaysNull(RecallStopReason stoppedBy)
    {
        RoundCapWarning.Build(Scan(stoppedBy, rounds: 2, lastWindow: 12), limit: 5).ShouldBeNull();
    }

    [Fact]
    public void Build_RoundCapShortPage_NamesLimitRoundsAndLastWindow()
    {
        var warnings = RoundCapWarning.Build(
            Scan(RecallStopReason.RoundCapReached, rounds: 4, lastWindow: 48),
            limit: 2);

        warnings.ShouldNotBeNull();
        warnings!.ShouldHaveSingleItem();
        warnings[0].ShouldBe(
            "recall page came up short after 4 rounds of window doubling: " +
            "1 of limit 2 matches, last window 48 points — narrow the query; " +
            "a wider limit only widens the searched window, it cannot fill the page");
    }

    private static RecallEscalationResult Scan(RecallStopReason stoppedBy, int rounds, int lastWindow) => new()
    {
        Kept = [new VectorMatch { Record = TestMemories.Create("one kept row", [0.9f, 0.4359f, 0f]), Similarity = 0.9 }],
        Rounds = rounds,
        LastWindow = lastWindow,
        StoppedBy = stoppedBy,
    };
}
