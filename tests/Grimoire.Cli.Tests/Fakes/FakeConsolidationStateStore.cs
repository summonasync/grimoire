using Grimoire.Core;

namespace Grimoire.Cli.Tests.Fakes;

public sealed class FakeConsolidationStateStore(ConsolidationState? initial = null) : IConsolidationStateStore
{
    public ConsolidationState Current { get; set; } = initial ?? ConsolidationState.Unknown("consolidation state file does not exist yet");

    public Task<ConsolidationState> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Current);

    public Task WriteAsync(ConsolidationState state, CancellationToken cancellationToken)
    {
        Current = state;
        return Task.CompletedTask;
    }
}
