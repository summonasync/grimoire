using Grimoire.Core;

namespace Grimoire.Api.Tomes;

public sealed class ApiTomeResolver(TomeResolver tomes, IActorClaimProvider? claims, string? envTome, string? envActor)
{
    public const string TomeEnvironmentVariable = "GRIMOIRE_TOME";

    public const string ActorEnvironmentVariable = "GRIMOIRE_ACTOR";

    public async Task<TomeResolution> ResolveIdentityAsync(string? explicitTome, CancellationToken cancellationToken)
    {
        var claim = explicitTome is null && claims is not null
            ? await claims.ReadAsync(cancellationToken)
            : null;
        return tomes.Resolve(explicitTome, envTome, AliasPart(envActor), claim);
    }

    public async Task<string> ResolveAsync(string? explicitTome, CancellationToken cancellationToken) =>
        (await ResolveIdentityAsync(explicitTome, cancellationToken)).ResolvedTome;

    private static string? AliasPart(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var separator = candidate.IndexOf('+', StringComparison.Ordinal);
        return separator < 0 ? candidate : candidate[..separator];
    }
}
