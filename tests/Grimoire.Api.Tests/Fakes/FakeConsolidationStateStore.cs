using Grimoire.Core;

namespace Grimoire.Api.Tests.Fakes;

public sealed class FakeConsolidationStateStore(ConsolidationState? initial = null) : IConsolidationStateStore
{
    public ConsolidationState Current { get; set; } = initial ?? ConsolidationState.Unknown("consolidation state file does not exist yet");

    public int WriteAttempts { get; private set; }

    public IReadOnlyList<ConsolidationState> Writes => _writes;

    private readonly List<ConsolidationState> _writes = [];

    public Task<ConsolidationState> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Current);

    public Task WriteAsync(ConsolidationState state, CancellationToken cancellationToken)
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
        Current = ConsolidationState.Unknown("consolidation state file does not exist yet");
        _writes.Clear();
        WriteAttempts = 0;
        FailWrites = false;
    }
}
