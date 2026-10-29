using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

internal sealed class FakeAgentIdentitySource(MemoryActor? identity = null) : IAgentIdentitySource
{
    public MemoryActor? Get() => identity;
}
