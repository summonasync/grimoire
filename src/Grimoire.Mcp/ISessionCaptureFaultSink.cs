namespace Grimoire.Mcp;

public interface ISessionCaptureFaultSink
{
    void CaptureFailed(string reason);
}
