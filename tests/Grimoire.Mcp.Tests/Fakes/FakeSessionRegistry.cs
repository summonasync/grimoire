using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

internal sealed class FakeSessionRegistry : ISessionRegistry
{
    private readonly object _gate = new();
    private readonly TaskCompletionSource<SessionRegistryEntry> _attempted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly List<SessionRegistryEntry> _written = [];

    private int attempts;

    public Exception? Failure { get; set; }

    public bool BlockWrites { get; set; }

    public string? DegradationReason { get; private set; }

    public IReadOnlyList<SessionRegistryEntry> Written
    {
        get { lock (_gate) return [.. _written]; }
    }

    public int Attempts
    {
        get { lock (_gate) return attempts; }
    }

    public Task<SessionRegistryEntry> AttemptedAsync => _attempted.Task;

    public async Task WriteAsync(SessionRegistryEntry entry, CancellationToken cancellationToken)
    {
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
    }

    public Task<IReadOnlyList<SessionRegistryEntry>> ListRecentAsync(
        int limit,
        DateTimeOffset? notBefore,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SessionRegistryEntry>>(Written);

    public Task<int> PruneAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        Task.FromResult(0);
}
