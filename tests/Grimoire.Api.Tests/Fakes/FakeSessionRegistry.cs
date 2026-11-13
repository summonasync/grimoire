using Grimoire.Core;

namespace Grimoire.Api.Tests.Fakes;

public sealed class FakeSessionRegistry : ISessionRegistry
{
    private readonly List<SessionRegistryEntry> _entries = [];

    public string? DegradationReason { get; set; }

    public int? LastLimit { get; private set; }

    public DateTimeOffset? LastNotBefore { get; private set; }

    public IReadOnlyList<SessionRegistryEntry> Entries => _entries;

    public void Seed(SessionRegistryEntry entry) => _entries.Add(entry);

    public void Clear()
    {
        _entries.Clear();
        LastLimit = null;
        LastNotBefore = null;
    }

    public Task<Guid> WriteAsync(SessionRegistryEntry entry, CancellationToken cancellationToken)
    {
        _entries.Add(entry);
        return Task.FromResult(Guid.CreateVersion7());
    }

    public Task RefineActorAsync(Guid id, string alias, string vendor, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<SessionRegistryEntry>> ListRecentAsync(
        int limit,
        DateTimeOffset? notBefore,
        CancellationToken cancellationToken)
    {
        LastLimit = limit;
        LastNotBefore = notBefore;

        IReadOnlyList<SessionRegistryEntry> rows = _entries
            .Where(entry => notBefore is null || entry.OccurredAt >= notBefore)
            .OrderByDescending(entry => entry.OccurredAt)
            .Take(limit)
            .ToArray();

        return Task.FromResult(rows);
    }

    public Task<int> PruneAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var expired = _entries.Where(entry => entry.OccurredAt < cutoff).ToArray();
        foreach (var entry in expired)
        {
            _entries.Remove(entry);
        }

        return Task.FromResult(expired.Length);
    }
}
