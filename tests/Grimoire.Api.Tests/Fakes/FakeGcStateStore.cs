using Grimoire.Core;

namespace Grimoire.Api.Tests.Fakes;

public sealed class FakeGcStateStore(GcState? initial = null) : IGcStateStore
{
    public GcState Current { get; set; } = initial ?? GcState.Unknown("gc state file does not exist yet");

    public int WriteAttempts { get; private set; }

    public IReadOnlyList<GcState> Writes => _writes;

    private readonly List<GcState> _writes = [];

    public Task<GcState> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Current);

    public Task WriteAsync(GcState state, CancellationToken cancellationToken)
    {
        WriteAttempts++;
        if (FailWrites)
        {
            throw new IOException("disk full");
        }

        _writes.Add(state);
        Current = state;
        return Task.CompletedTask;
    }

    public bool FailWrites { get; set; }

    public void Reset()
    {
        Current = GcState.Unknown("gc state file does not exist yet");
        _writes.Clear();
        WriteAttempts = 0;
        FailWrites = false;
    }
}
