using Grimoire.Core;

namespace Grimoire.Core.Tests.Fakes;

public sealed class FakeConsolidationStateStore(ConsolidationState? initial = null) : IConsolidationStateStore
{
    public ConsolidationState Current { get; set; } = initial ?? ConsolidationState.Unknown("consolidation state file does not exist yet");

    public IReadOnlyList<ConsolidationState> Writes => _writes;

    private readonly List<ConsolidationState> _writes = [];

    public Task<ConsolidationState> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Current);

    public Task WriteAsync(ConsolidationState state, CancellationToken cancellationToken)
    {
        _writes.Add(state);
        Current = state;
        return Task.CompletedTask;
    }
}
