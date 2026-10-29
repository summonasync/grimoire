using Grimoire.Core;

namespace Grimoire.Mcp;

internal sealed class UnsetAgentIdentitySource : IAgentIdentitySource
{
    public MemoryActor? Get() => null;
}
