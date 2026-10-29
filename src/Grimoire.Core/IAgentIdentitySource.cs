namespace Grimoire.Core;

public interface IAgentIdentitySource
{
    MemoryActor? Get();
}
