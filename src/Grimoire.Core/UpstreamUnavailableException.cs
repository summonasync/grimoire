namespace Grimoire.Core;

public sealed class UpstreamUnavailableException(string upstream, Exception? innerException = null)
    : Exception($"Upstream '{upstream}' is unavailable.", innerException)
{
    public string Upstream { get; } = upstream;
}
