using System.Globalization;
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
    public void ResolveZone_UnknownId_FallsBackToUtcWithoutThrowing(string timeZoneId)
    {
        var zone = DailyWindowResolver.ResolveZone(timeZoneId).Zone;

        zone.ShouldBeSameAs(TimeZoneInfo.Utc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveZone_MissingId_DefaultsToTheMachineLocalZone(string? timeZoneId)
    {
        var zone = DailyWindowResolver.ResolveZone(timeZoneId).Zone;
        var machineLocal = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneInfo.Local.Id);

        zone.Id.ShouldBe(machineLocal.Id);
        zone.BaseUtcOffset.ShouldBe(machineLocal.BaseUtcOffset);

        if (machineLocal.Id != "UTC")
        {
            zone.ShouldNotBeSameAs(TimeZoneInfo.Utc);
        }
    }

    [Fact]
    public void Resolve_OptionsWithoutZone_BuildWindowsOfTheLocallyDerivedZone()
    {
        var now = new DateTimeOffset(2026, 11, 12, 20, 30, 0, TimeSpan.Zero);
        var expected = PlanAt(now, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneInfo.Local.Id));

        var plan = Resolver(now, new MemoryEngineOptions()).Resolve();

        plan.Today.ShouldBe(expected.Today);
        plan.LastWorkingDayCandidates.ShouldBe(expected.LastWorkingDayCandidates);
        plan.Today.CreatedAfter.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Resolve_OptionsWithExplicitZone_WinOverTheMachineDefault()
    {
        var now = new DateTimeOffset(2026, 11, 12, 20, 30, 0, TimeSpan.Zero);
        var kyiv = DailyWindowResolver.ResolveZone("Europe/Kyiv").Zone;
        var expected = PlanAt(now, kyiv);
        var machineDefault = PlanAt(now, DailyWindowResolver.ResolveZone(null).Zone);

        var plan = Resolver(now, new MemoryEngineOptions { DailyTimeZone = "Europe/Kyiv" }).Resolve();

        plan.Today.ShouldBe(expected.Today);
        if (expected.Today.CreatedAfter != machineDefault.Today.CreatedAfter)
        {
            plan.Today.CreatedAfter.ShouldNotBe(machineDefault.Today.CreatedAfter);
        }
    }

    [Theory]
    [InlineData("2026-03-29T12:00:00Z", "2026-03-29", 23, "2026-03-28T22:00:00Z", "2026-03-29T21:00:00Z")]
    [InlineData("2026-10-25T12:00:00Z", "2026-10-25", 25, "2026-10-24T21:00:00Z", "2026-10-25T22:00:00Z")]
    public void Resolve_KyivTransitionDay_WindowAndNeighboursFollowTheClockShift(
        string nowIso,
        string dayIso,
        int windowHours,
        string afterIso,
        string beforeIso)
    {
        var zone = DailyWindowResolver.ResolveZone("Europe/Kyiv").Zone;
        zone.Id.ShouldBe("Europe/Kyiv");

        var plan = PlanAt(DateTimeOffset.Parse(nowIso, CultureInfo.InvariantCulture), zone);
        var previousDay = plan.LastWorkingDayCandidates[0];

        plan.Today.Day.ShouldBe(DateOnly.ParseExact(dayIso, "yyyy-MM-dd", CultureInfo.InvariantCulture));
        plan.Today.CreatedAfter.ShouldBe(DateTimeOffset.Parse(afterIso, CultureInfo.InvariantCulture));
        plan.Today.CreatedBefore.ShouldBe(DateTimeOffset.Parse(beforeIso, CultureInfo.InvariantCulture));
        (plan.Today.CreatedBefore - plan.Today.CreatedAfter).TotalHours.ShouldBe(windowHours);
        (previousDay.CreatedBefore - previousDay.CreatedAfter).TotalHours.ShouldBe(24);
        plan.Today.CreatedAfter.ShouldBe(previousDay.CreatedBefore);

        var windows = new[] { plan.Today }.Concat(plan.LastWorkingDayCandidates).ToList();
        windows.ShouldAllBe(window => window.CreatedBefore > window.CreatedAfter);
        windows.ShouldAllBe(window => window.CreatedAfter.Offset == TimeSpan.Zero
            && window.CreatedBefore.Offset == TimeSpan.Zero);

        for (var index = 0; index < windows.Count - 1; index++)
        {
            windows[index].CreatedAfter.ShouldBe(windows[index + 1].CreatedBefore);
        }
    }

    [Fact]
    public void ResolveZone_KnownSystemId_ResolvesThatZone()
    {
        var zone = DailyWindowResolver.ResolveZone("Europe/Moscow").Zone;

        zone.BaseUtcOffset.ShouldBe(TimeSpan.FromHours(3));
    }

    [Theory]
    [InlineData("Europe/Moscow", DailyZoneSource.Configured)]
    [InlineData("Not/AZone", DailyZoneSource.UtcFallback)]
    [InlineData("Mars/Olympus_Mons", DailyZoneSource.UtcFallback)]
    [InlineData("", DailyZoneSource.MachineLocal)]
    [InlineData("   ", DailyZoneSource.MachineLocal)]
    [InlineData(null, DailyZoneSource.MachineLocal)]
    public void ResolveZone_ReportsWhichBranchItTook(string? timeZoneId, DailyZoneSource expected)
    {
        var outcome = DailyWindowResolver.ResolveZone(timeZoneId);

        outcome.Source.ShouldBe(expected);

        if (expected == DailyZoneSource.UtcFallback)
        {
            outcome.Zone.ShouldBeSameAs(TimeZoneInfo.Utc);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveZone_DefaultPathNeverClaimsFallbackEvenWhenTheHostIdIsBroken(string? timeZoneId)
    {
        var outcome = DailyWindowResolver.ResolveZone(timeZoneId);
        var hostIdResolves = TimeZoneInfo.TryFindSystemTimeZoneById(TimeZoneInfo.Local.Id, out _);

        outcome.Source.ShouldBe(DailyZoneSource.MachineLocal);

        if (!hostIdResolves)
        {
            outcome.Zone.ShouldBeSameAs(TimeZoneInfo.Utc);
        }
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
        plan.Today.CreatedAfter.Offset.ShouldBe(TimeSpan.Zero);
        plan.Today.CreatedBefore.Offset.ShouldBe(TimeSpan.Zero);
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
