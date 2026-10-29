using Grimoire.Core;

namespace Grimoire.Cli.Tests.Fakes;

public sealed class FixedIdentitySource(MemoryActor? identity) : IAgentIdentitySource
{
    public MemoryActor? Get() => identity;
}
