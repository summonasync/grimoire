using Grimoire.Core;

namespace Grimoire.Mcp;

internal sealed class SessionIdentityHolder
{
    private readonly object _gate = new();

    private MemoryActor? _identity;

    public void Set(MemoryActor identity)
    {
        lock (_gate)
        {
            _identity = identity;
        }
    }

    public MemoryActor? Read()
    {
        lock (_gate)
        {
            return _identity;
        }
    }
}
