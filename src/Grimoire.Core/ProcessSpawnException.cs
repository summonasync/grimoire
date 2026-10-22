namespace Grimoire.Core;

public sealed class ProcessSpawnException(string file, string reason, Exception? innerException = null)
    : Exception($"Failed to spawn '{file}': {reason}.", innerException)
{
    public string File { get; } = file;
}
