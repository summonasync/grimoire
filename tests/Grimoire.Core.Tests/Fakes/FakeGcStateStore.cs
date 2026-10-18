using Grimoire.Core;

namespace Grimoire.Core.Tests.Fakes;

public sealed class FakeGcStateStore(GcState? initial = null) : IGcStateStore
{
    public GcState Current { get; set; } = initial ?? GcState.Unknown("gc state file does not exist yet");

    public IReadOnlyList<GcState> Writes => _writes;

    private readonly List<GcState> _writes = [];

    public Task<GcState> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Current);

    public Task WriteAsync(GcState state, CancellationToken cancellationToken)
    {
        _writes.Add(state);
        Current = state;
        return Task.CompletedTask;
    }
}
