namespace Grimoire.Core;

public interface IActorClaimProvider
{
    Task<ActorClaim?> ReadAsync(CancellationToken cancellationToken);
}
