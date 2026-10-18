namespace Grimoire.Core;

public interface IGcStateStore
{
    Task<GcState> ReadAsync(CancellationToken cancellationToken);

    Task WriteAsync(GcState state, CancellationToken cancellationToken);
}
