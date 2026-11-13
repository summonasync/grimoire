namespace Grimoire.Mcp;

internal sealed class StderrSessionCaptureFaultSink : ISessionCaptureFaultSink
{
    public const string FaultPrefix = "session_capture_failed";

    public void CaptureFailed(string reason) =>
        Console.Error.WriteLine($"{FaultPrefix} {Flatten(reason)}");

    private static string Flatten(string detail) => detail
        .Replace('\r', ' ')
        .Replace('\n', ' ');
}
