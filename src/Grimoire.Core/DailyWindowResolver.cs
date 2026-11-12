namespace Grimoire.Core;

public sealed class DailyWindowResolver(MemoryEngineOptions options, IClock clock)
{
    public DailyWindowPlan Resolve() => Resolve(ResolveZone(options.DailyTimeZone).Zone);

    public DailyWindowPlan Resolve(TimeZoneInfo zone)
    {
        var today = LocalDay(clock.UtcNow, zone);

        return new DailyWindowPlan
        {
            Today = Window(today, zone, BriefDailyBucket.Today),
            LastWorkingDayCandidates = [.. Candidates(today, zone)],
        };
    }

    public static DailyZoneResolution ResolveZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return new DailyZoneResolution
            {
                Zone = ResolveSystemZone(TimeZoneInfo.Local.Id),
                Source = DailyZoneSource.MachineLocal,
            };
        }

        return TryResolveSystemZone(timeZoneId) is { } configured
            ? new DailyZoneResolution { Zone = configured, Source = DailyZoneSource.Configured }
            : new DailyZoneResolution { Zone = TimeZoneInfo.Utc, Source = DailyZoneSource.UtcFallback };
    }

    private static TimeZoneInfo ResolveSystemZone(string timeZoneId) =>
        TryResolveSystemZone(timeZoneId) ?? TimeZoneInfo.Utc;

    private static TimeZoneInfo? TryResolveSystemZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception failure) when (failure is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }

    private IEnumerable<DailyWindow> Candidates(DateOnly today, TimeZoneInfo zone)
    {
        for (var back = 1; back <= Math.Max(0, options.DailyLookbackDays); back++)
        {
            yield return Window(today.AddDays(-back), zone, BriefDailyBucket.LastWorkingDay);
        }
    }

    private static DailyWindow Window(DateOnly day, TimeZoneInfo zone, BriefDailyBucket bucket) => new()
    {
        Day = day,
        Bucket = bucket,
        CreatedAfter = LocalMidnight(day, zone),
        CreatedBefore = LocalMidnight(day.AddDays(1), zone),
    };

    private static DateOnly LocalDay(DateTimeOffset instant, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(instant, zone);
        return new DateOnly(local.Year, local.Month, local.Day);
    }

    private static DateTimeOffset LocalMidnight(DateOnly day, TimeZoneInfo zone)
    {
        var midnight = new DateTime(day.Year, day.Month, day.Day, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(midnight, zone.GetUtcOffset(midnight)).ToUniversalTime();
    }
}
