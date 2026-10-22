namespace Grimoire.Core;

public interface IExternalProcess
{
    Task<int> RunAsync(string file, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
