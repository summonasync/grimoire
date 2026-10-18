using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class GcStateTests
{
    private static readonly TimeSpan Threshold = TimeSpan.FromHours(2);

    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsStale_AtExactlyTheThreshold_IsFalse()
    {
        var state = new GcState { LastSuccessAt = Now - Threshold };

        state.IsStale(Threshold, Now).ShouldBeFalse();
    }

    [Fact]
    public void IsStale_OneTickPastTheThreshold_IsTrue()
    {
        var state = new GcState { LastSuccessAt = Now - Threshold - TimeSpan.FromTicks(1) };

        state.IsStale(Threshold, Now).ShouldBeTrue();
    }

    [Fact]
    public void IsStale_FreshSuccess_IsFalse()
    {
        var state = new GcState { LastSuccessAt = Now.AddMinutes(-1) };

        state.IsStale(Threshold, Now).ShouldBeFalse();
    }

    [Fact]
    public void IsStale_WithoutAnySuccess_IsTrue()
    {
        new GcState().IsStale(Threshold, Now).ShouldBeTrue();
    }

    [Fact]
    public void IsStale_UnknownState_IsTrue()
    {
        GcState.Unknown("gc state file does not exist yet").IsStale(Threshold, Now).ShouldBeTrue();
    }

    [Fact]
    public void Unknown_CarriesTheReasonAndIsFlagged()
    {
        var state = GcState.Unknown("gc state file is not valid JSON: boom");

        state.IsUnknown.ShouldBeTrue();
        state.UnknownReason.ShouldBe("gc state file is not valid JSON: boom");
        state.LastSuccessAt.ShouldBeNull();
    }

    [Fact]
    public void KnownState_IsNotFlaggedUnknown()
    {
        var state = new GcState { LastAttemptAt = Now, LastSuccessAt = Now, LastError = null };

        state.IsUnknown.ShouldBeFalse();
    }
}
