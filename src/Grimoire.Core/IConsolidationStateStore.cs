namespace Grimoire.Core;

public interface IConsolidationStateStore
{
    Task<ConsolidationState> ReadAsync(CancellationToken cancellationToken);

    Task WriteAsync(ConsolidationState state, CancellationToken cancellationToken);
}
