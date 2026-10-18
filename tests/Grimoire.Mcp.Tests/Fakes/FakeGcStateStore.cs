using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

internal sealed class FakeGcStateStore(GcState? initial = null) : IGcStateStore
{
    public GcState Current { get; set; } = initial ?? GcState.Unknown("gc state file does not exist yet");

    public Task<GcState> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Current);

    public Task WriteAsync(GcState state, CancellationToken cancellationToken)
    {
        Current = state;
        return Task.CompletedTask;
    }
}
