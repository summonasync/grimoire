using Grimoire.Api.Contracts;
using Grimoire.Core;

namespace Grimoire.Api.Sessions;

public sealed class SessionHandshakeProjection(ISessionRegistry registry, IClock clock, double retentionHours)
{
    public const int DefaultLimit = 50;

    public const int MinLimit = 1;

    public const int MaxLimit = 500;

    public const double MinWindowHours = 0;

    public const double MaxWindowHours = 8760;

    private readonly double _retentionHours = ValidateRetentionDefault(retentionHours);

    public async Task<SessionsResponse> ProjectAsync(
        int? limit,
        double? windowHours,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, MinLimit, MaxLimit);
        var askedWindow = windowHours is { } raw && double.IsFinite(raw) ? raw : _retentionHours;
        var effectiveWindow = Math.Clamp(askedWindow, MinWindowHours, MaxWindowHours);
        var now = clock.UtcNow;
        var rows = await registry.ListRecentAsync(
            effectiveLimit,
            now.AddHours(-effectiveWindow),
            cancellationToken);
        var items = rows.Select(entry => Item(entry, now)).ToList();

        return new SessionsResponse(items, effectiveLimit, effectiveWindow, registry.DegradationReason);
    }

    private static SessionHandshakeItem Item(SessionRegistryEntry entry, DateTimeOffset now) =>
        new(
            entry.ClientName,
            entry.ClientVersion,
            entry.ProtocolVersion,
            entry.ActorAlias,
            entry.ActorVendor,
            entry.OccurredAt,
            Math.Round((now - entry.OccurredAt).TotalHours, 2),
            entry.Host);

    private static double ValidateRetentionDefault(double retentionHours)
    {
        if (!double.IsFinite(retentionHours))
        {
            throw new ArgumentOutOfRangeException(
                nameof(retentionHours),
                retentionHours,
                "The default handshake window must be a finite number of hours — a non-finite default survives the argument guard " +
                "and reaches the response as a window no JSON writer can emit.");
        }

        return retentionHours;
    }
}
