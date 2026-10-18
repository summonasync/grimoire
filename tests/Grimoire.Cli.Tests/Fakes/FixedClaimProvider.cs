using Grimoire.Core;

namespace Grimoire.Cli.Tests.Fakes;

public sealed class FixedClaimProvider(ActorClaim? claim) : IActorClaimProvider
{
    public int Reads { get; private set; }

    public Task<ActorClaim?> ReadAsync(CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(claim);
    }
}
