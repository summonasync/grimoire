using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class DailyWindowResolverTests
{
    private static readonly TimeZoneInfo PlusThree = TimeZoneInfo.CreateCustomTimeZone(
        "test-utc-plus-three", TimeSpan.FromHours(3), "UTC+03:00", "UTC+03:00");

    private static readonly TimeZoneInfo MinusEight = TimeZoneInfo.CreateCustomTimeZone(
        "test-utc-minus-eight", TimeSpan.FromHours(-8), "UTC-08:00", "UTC-08:00");

    private static readonly DateTimeOffset MondayMorningUtc =
        new(2026, 11, 9, 7, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ThursdayLocalEveningUtc =
        new(2026, 11, 5, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Resolve_ClockAt2030UtcInPlusThreeZone_TodayWindowPinsLocalDayAndUtcBounds()
    {
        var plan = PlanAt(new DateTimeOffset(2026, 11, 12, 20, 30, 0, TimeSpan.Zero), PlusThree);

        plan.Today.Day.ShouldBe(new DateOnly(2026, 11, 12));
        plan.Today.Bucket.ShouldBe(BriefDailyBucket.Today);
        plan.Today.CreatedAfter.ShouldBe(new DateTimeOffset(2026, 11, 11, 21, 0, 0, TimeSpan.Zero));
        plan.Today.CreatedBefore.ShouldBe(new DateTimeOffset(2026, 11, 12, 21, 0, 0, TimeSpan.Zero));
        plan.Today.CreatedAfter.Offset.ShouldBe(TimeSpan.Zero);
        plan.Today.CreatedBefore.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void TodayWindow_NoteAt2330LocalIsHeldAndNoteAtNextLocalMidnightIsNot()
    {
        var plan = PlanAt(new DateTimeOffset(2026, 11, 12, 20, 30, 0, TimeSpan.Zero), PlusThree);

        Holds(plan.Today, Note(new DateTimeOffset(2026, 11, 12, 20, 30, 0, TimeSpan.Zero))).ShouldBeTrue();
        Holds(plan.Today, Note(new DateTimeOffset(2026, 11, 12, 20, 59, 59, TimeSpan.Zero))).ShouldBeTrue();
        Holds(plan.Today, Note(new DateTimeOffset(2026, 11, 11, 20, 59, 59, TimeSpan.Zero))).ShouldBeFalse();
        Holds(plan.Today, Note(new DateTimeOffset(2026, 11, 12, 21, 0, 0, TimeSpan.Zero))).ShouldBeFalse();
    }

    [Fact]
    public void Resolve_ZoneBehindUtc_TodayIsTheLocalCalendarDayNotTheUtcOne()
    {
        var plan = PlanAt(new DateTimeOffset(2026, 11, 12, 2, 0, 0, TimeSpan.Zero), MinusEight);

        plan.Today.Day.ShouldBe(new DateOnly(2026, 11, 11));
        plan.Today.CreatedAfter.ShouldBe(new DateTimeOffset(2026, 11, 11, 8, 0, 0, TimeSpan.Zero));
        plan.Today.CreatedBefore.ShouldBe(new DateTimeOffset(2026, 11, 12, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Resolve_SevenDayLookback_CandidatesArePriorLocalDaysNewestFirst()
    {
        var plan = PlanAt(MondayMorningUtc, PlusThree);

        plan.LastWorkingDayCandidates.Select(window => window.Day).ToList().ShouldBe(
        [
            new DateOnly(2026, 11, 8),
            new DateOnly(2026, 11, 7),
            new DateOnly(2026, 11, 6),
            new DateOnly(2026, 11, 5),
            new DateOnly(2026, 11, 4),
            new DateOnly(2026, 11, 3),
            new DateOnly(2026, 11, 2),
        ]);

        plan.LastWorkingDayCandidates.ShouldAllBe(
            window => window.Bucket == BriefDailyBucket.LastWorkingDay);
    }

    [Fact]
    public void Resolve_AllWindowsFromTodayToOldestCandidate_AreContiguousDayBlocks()
    {
        var plan = PlanAt(MondayMorningUtc, PlusThree);
        var windows = new[] { plan.Today }.Concat(plan.LastWorkingDayCandidates).ToList();

        windows.ShouldAllBe(window => window.CreatedBefore > window.CreatedAfter);

        for (var index = 0; index < windows.Count - 1; index++)
        {
            windows[index].CreatedAfter.ShouldBe(windows[index + 1].CreatedBefore);
        }
    }

    [Fact]
    public void FirstCandidateHoldingNotes_MondayAfterNotelessWeekend_PicksFriday()
    {
        var plan = PlanAt(MondayMorningUtc, PlusThree);
        var friday = LocalInPlusThree(2026, 11, 6, 9);

        var picked = FirstCandidateHolding(plan, Note(ThursdayLocalEveningUtc), Note(friday));

        picked.ShouldNotBeNull();
        picked.Day.ShouldBe(new DateOnly(2026, 11, 6));
        picked.Bucket.ShouldBe(BriefDailyBucket.LastWorkingDay);
        picked.CreatedAfter.ShouldBe(new DateTimeOffset(2026, 11, 5, 21, 0, 0, TimeSpan.Zero));
        picked.CreatedBefore.ShouldBe(new DateTimeOffset(2026, 11, 6, 21, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void FirstCandidateHoldingNotes_NoteEightLocalDaysBack_PicksNothing()
    {
        var plan = PlanAt(MondayMorningUtc, PlusThree);
        var eightDaysBack = LocalInPlusThree(2026, 11, 1, 9);

        FirstCandidateHolding(plan, Note(eightDaysBack)).ShouldBeNull();
        plan.LastWorkingDayCandidates.Count.ShouldBe(7);
        plan.LastWorkingDayCandidates.Last().Day.ShouldBe(new DateOnly(2026, 11, 2));
        Holds(plan.Today, Note(eightDaysBack)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Not/AZone")]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ResolveZone_UnknownOrBlankId_FallsBackToUtcWithoutThrowing(string? timeZoneId)
    {
        var zone = DailyWindowResolver.ResolveZone(timeZoneId);

        zone.ShouldBeSameAs(TimeZoneInfo.Utc);
        zone.BaseUtcOffset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void ResolveZone_KnownSystemId_ResolvesThatZone()
    {
        var zone = DailyWindowResolver.ResolveZone("Europe/Moscow");

        zone.BaseUtcOffset.ShouldBe(TimeSpan.FromHours(3));
    }

    [Fact]
    public void Resolve_OptionsCarryingGarbageZone_BuildsUtcDayWindows()
    {
        var resolver = Resolver(
            new DateTimeOffset(2026, 11, 12, 20, 30, 0, TimeSpan.Zero),
            new MemoryEngineOptions { DailyTimeZone = "Not/AZone" });

        var plan = resolver.Resolve();

        plan.Today.Day.ShouldBe(new DateOnly(2026, 11, 12));
        plan.Today.CreatedAfter.ShouldBe(new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero));
        plan.Today.CreatedBefore.ShouldBe(new DateTimeOffset(2026, 11, 13, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Resolve_OptionsCarryingMoscowZone_BuildsThatZonesLocalDay()
    {
        var resolver = Resolver(
            new DateTimeOffset(2026, 11, 12, 20, 30, 0, TimeSpan.Zero),
            new MemoryEngineOptions { DailyTimeZone = "Europe/Moscow" });

        var plan = resolver.Resolve();

        plan.Today.CreatedAfter.ShouldBe(new DateTimeOffset(2026, 11, 11, 21, 0, 0, TimeSpan.Zero));
        plan.Today.CreatedBefore.ShouldBe(new DateTimeOffset(2026, 11, 12, 21, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Resolve_ZeroLookback_ReturnsTodayWithNoCandidates()
    {
        var resolver = Resolver(MondayMorningUtc, new MemoryEngineOptions { DailyLookbackDays = 0 });
        var plan = resolver.Resolve(PlusThree);

        plan.LastWorkingDayCandidates.ShouldBeEmpty();
    }

    private static DailyWindowPlan PlanAt(DateTimeOffset now, TimeZoneInfo zone, int lookbackDays = 7) =>
        Resolver(now, new MemoryEngineOptions { DailyLookbackDays = lookbackDays }).Resolve(zone);

    private static DailyWindowResolver Resolver(DateTimeOffset now, MemoryEngineOptions options) =>
        new(options, new FakeClock(now));

    private static MemoryRecord Note(DateTimeOffset created) =>
        TestMemories.Create("daily note", [1f, 0f], kind: MemoryKind.Journal, created: created);

    private static bool Holds(DailyWindow window, MemoryRecord note) =>
        new MemoryFilter
            {
                CreatedAfter = window.CreatedAfter,
                CreatedBefore = window.CreatedBefore,
            }
            .Matches(note);

    private static DailyWindow? FirstCandidateHolding(DailyWindowPlan plan, params MemoryRecord[] notes) =>
        plan.LastWorkingDayCandidates.FirstOrDefault(window => notes.Any(note => Holds(window, note)));

    private static DateTimeOffset LocalInPlusThree(int year, int month, int day, int hour) =>
        new(new DateTime(year, month, day, hour, 0, 0), TimeSpan.FromHours(3));
}
