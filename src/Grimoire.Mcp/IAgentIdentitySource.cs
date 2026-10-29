using Grimoire.Core;

namespace Grimoire.Mcp;

internal interface IAgentIdentitySource
{
    MemoryActor? Get();
}
