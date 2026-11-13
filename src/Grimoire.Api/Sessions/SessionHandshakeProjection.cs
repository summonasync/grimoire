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

    public async Task<SessionsResponse> ProjectAsync(
        int? limit,
        double? windowHours,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, MinLimit, MaxLimit);
        var effectiveWindow = Math.Clamp(windowHours ?? retentionHours, MinWindowHours, MaxWindowHours);
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
}
