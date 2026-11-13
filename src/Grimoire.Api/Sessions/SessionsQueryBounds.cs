using System.Globalization;

namespace Grimoire.Api.Sessions;

internal static class SessionsQueryBounds
{
    public static string? Rejection(int? limit, double? windowHours)
    {
        if (limit is { } rows
            && (rows < SessionHandshakeProjection.MinLimit || rows > SessionHandshakeProjection.MaxLimit))
        {
            return $"limit must be a whole number of rows between {SessionHandshakeProjection.MinLimit} and " +
                   $"{SessionHandshakeProjection.MaxLimit}; '{rows}' is out of range.";
        }

        if (windowHours is { } hours
            && (hours < SessionHandshakeProjection.MinWindowHours
                || hours > SessionHandshakeProjection.MaxWindowHours))
        {
            var candidate = hours.ToString("R", CultureInfo.InvariantCulture);
            return $"windowHours must be a finite number of hours between " +
                   $"{SessionHandshakeProjection.MinWindowHours} and {SessionHandshakeProjection.MaxWindowHours}; " +
                   $"'{candidate}' is out of range.";
        }

        return null;
    }
}
