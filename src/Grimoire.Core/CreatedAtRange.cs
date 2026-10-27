using System.Globalization;

namespace Grimoire.Core;

public readonly record struct CreatedAtRange(DateTimeOffset? Since, DateTimeOffset? Until)
{
    private const string DateOnlyFormat = "yyyy-MM-dd";

    public static CreatedAtRange Parse(string? since, string? until, TimeZoneInfo? zone = null)
    {
        var resolvedZone = zone ?? TimeZoneInfo.Local;
        var from = ResolveBound(since, "since", resolvedZone);
        var to = ResolveBound(until, "until", resolvedZone);

        if (from is { } start && to is { } end && start > end)
        {
            throw new ArgumentException("'since' must not be later than 'until'.", "since");
        }

        return new CreatedAtRange(from, to);
    }

    private static DateTimeOffset? ResolveBound(string? value, string parameter, TimeZoneInfo zone)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParseExact(value, DateOnlyFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return new DateTimeOffset(date, zone.GetUtcOffset(date)).ToUniversalTime();
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant))
        {
            return instant;
        }

        throw new ArgumentException(
            $"'{parameter}' must be an ISO-8601 timestamp or a yyyy-MM-dd date but was '{value}'.", parameter);
    }
}
