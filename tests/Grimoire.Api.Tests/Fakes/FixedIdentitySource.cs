using Grimoire.Core;

namespace Grimoire.Api.Tests.Fakes;

public sealed class FixedIdentitySource(MemoryActor? identity) : IAgentIdentitySource
{
    public MemoryActor? Get() => identity;
}
