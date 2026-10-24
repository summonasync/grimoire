using Grimoire.Api.Contracts;
using Grimoire.Core;

namespace Grimoire.Api.Agents;

public sealed class AgentActivityProjection(IMemoryStore store, IClock clock)
{
    public const int DefaultWindowDays = 30;

    public const int MaxWindowDays = 365;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private AgentActivityResponse? _cached;
    private DateTimeOffset _cachedAt;

    public async Task<AgentActivityResponse> ProjectAsync(int? windowDays, CancellationToken cancellationToken)
    {
        var window = Math.Clamp(windowDays ?? DefaultWindowDays, 1, MaxWindowDays);
        lock (_gate)
        {
            if (_cached is { } cached && cached.WindowDays == window && clock.UtcNow - _cachedAt < CacheTtl)
            {
                return cached;
            }
        }

        var response = await BuildAsync(window, cancellationToken);
        lock (_gate)
        {
            _cached = response;
            _cachedAt = clock.UtcNow;
        }

        return response;
    }

    // IMemoryStore directly, not MemoryService.ListAsync: that call resolves a null tome
    // to the default, while this panel is cross-tome by spec. Tombstoned writes still
    // count as past activity.
    private async Task<AgentActivityResponse> BuildAsync(int windowDays, CancellationToken cancellationToken)
    {
        var cutoff = clock.UtcNow.AddDays(-windowDays);
        var records = await store.ListAsync(new MemoryFilter { CreatedAfter = cutoff }, cancellationToken);
        var activity = new Dictionary<MemoryActor, (int Count, DateTimeOffset Last)>();

        foreach (var record in records)
        {
            var (count, last) = activity.TryGetValue(record.Actor, out var entry)
                ? entry
                : (0, DateTimeOffset.MinValue);
            activity[record.Actor] = (count + 1, record.CreatedAt > last ? record.CreatedAt : last);
        }

        var agents = activity
            .OrderByDescending(entry => entry.Value.Last)
            .ThenBy(entry => entry.Key.Alias, StringComparer.Ordinal)
            .Select(entry => new AgentActivityItem(
                entry.Key.Alias,
                entry.Key.Vendor,
                entry.Value.Count,
                entry.Value.Last,
                entry.Key.IsHuman))
            .ToList();

        return new AgentActivityResponse(agents, windowDays);
    }
}
