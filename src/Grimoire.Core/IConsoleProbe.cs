namespace Grimoire.Core;

public interface IConsoleProbe
{
    Task<bool> IsAliveAsync(int port, CancellationToken cancellationToken);
}
