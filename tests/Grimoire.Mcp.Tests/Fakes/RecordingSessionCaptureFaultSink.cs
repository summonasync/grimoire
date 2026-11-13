namespace Grimoire.Mcp.Tests;

internal sealed class RecordingSessionCaptureFaultSink : ISessionCaptureFaultSink
{
    private readonly TaskCompletionSource<string> _first =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines
    {
        get { lock (_lines) return [.. _lines]; }
    }

    public Task<string> FirstLineAsync => _first.Task;

    public void CaptureFailed(string reason)
    {
        lock (_lines)
        {
            _lines.Add(reason);
        }

        _first.TrySetResult(reason);
    }
}
