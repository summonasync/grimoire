using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

internal sealed class FakeActorClaimProvider(ActorClaim? claim = null) : IActorClaimProvider
{
    public int Reads { get; private set; }

    public Task<ActorClaim?> ReadAsync(CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(claim);
    }
}
