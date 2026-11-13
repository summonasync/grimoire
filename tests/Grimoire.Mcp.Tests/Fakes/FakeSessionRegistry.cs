using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

internal sealed class FakeSessionRegistry : ISessionRegistry
{
    private readonly object _gate = new();
    private readonly TaskCompletionSource<SessionRegistryEntry> _attempted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<(Guid Id, string Alias, string? Vendor)> _refined =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly List<SessionRegistryEntry> _written = [];

    private readonly List<Guid> _writtenIds = [];

    private readonly List<(Guid Id, string Alias, string? Vendor)> _refinedRows = [];

    private int attempts;

    public Exception? Failure { get; set; }

    public Exception? RefineFailure { get; set; }

    public bool BlockWrites { get; set; }

    public bool BlockRefinements { get; set; }

    public string? DegradationReason { get; private set; }

    public IReadOnlyList<SessionRegistryEntry> Written
    {
        get { lock (_gate) return [.. _written]; }
    }

    public IReadOnlyList<Guid> StoredIds
    {
        get { lock (_gate) return [.. _writtenIds]; }
    }

    public IReadOnlyList<(Guid Id, string Alias, string? Vendor)> Refinements
    {
        get { lock (_gate) return [.. _refinedRows]; }
    }

    public int Attempts
    {
        get { lock (_gate) return attempts; }
    }

    public Task<SessionRegistryEntry> AttemptedAsync => _attempted.Task;

    public Task<(Guid Id, string Alias, string? Vendor)> FirstRefinementAsync => _refined.Task;

    public async Task<Guid> WriteAsync(SessionRegistryEntry entry, CancellationToken cancellationToken)
    {
        var id = Guid.CreateVersion7();

        lock (_gate)
        {
            attempts++;
            _written.Add(entry);
        }

        _attempted.TrySetResult(entry);

        if (Failure is { } failure)
        {
            throw failure;
        }

        if (BlockWrites)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        lock (_gate)
        {
            _writtenIds.Add(id);
        }

        return id;
    }

    public async Task RefineActorAsync(Guid id, string alias, string? vendor, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _refinedRows.Add((id, alias, vendor));
        }

        _refined.TrySetResult((id, alias, vendor));

        if (RefineFailure is { } failure)
        {
            throw failure;
        }

        if (BlockRefinements)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    public Task<IReadOnlyList<SessionRegistryEntry>> ListRecentAsync(
        int limit,
        DateTimeOffset? notBefore,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SessionRegistryEntry>>(Written);

    public Task<int> PruneAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        Task.FromResult(0);
}
