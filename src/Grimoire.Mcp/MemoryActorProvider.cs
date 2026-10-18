using Grimoire.Core;

namespace Grimoire.Mcp;

internal sealed class MemoryActorProvider(MemoryActor? configured, ClientInfoHolder clientInfo)
{
    private static readonly MemoryActor UnknownActor = new("unknown", null);

    public MemoryActor GetActor()
    {
        if (configured.HasValue)
        {
            return configured.Value;
        }

        var clientName = clientInfo.Read()?.Name;
        return string.IsNullOrWhiteSpace(clientName) ? UnknownActor : new MemoryActor(clientName, null);
    }
}
