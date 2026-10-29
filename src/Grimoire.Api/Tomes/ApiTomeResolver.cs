using Grimoire.Core;

namespace Grimoire.Api.Tomes;

public sealed class ApiTomeResolver(
    TomeResolver tomes,
    IActorClaimProvider? claims,
    string? envTome,
    string? envActor,
    IAgentIdentitySource? identityFiles = null)
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

    public async Task<MemoryActor> ResolveActorAsync(CancellationToken cancellationToken)
    {
        if (identityFiles?.Get() is { } fileActor)
        {
            return fileActor;
        }

        if (ParseActor(envActor) is { } configured)
        {
            return configured;
        }

        var claim = claims is null ? null : await claims.ReadAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(claim?.Alias)
            ? MemoryActor.Human
            : new MemoryActor(claim!.Alias, string.IsNullOrWhiteSpace(claim.Vendor) ? null : claim.Vendor);
    }

    private static string? AliasPart(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var separator = candidate.IndexOf('+', StringComparison.Ordinal);
        return separator < 0 ? candidate : candidate[..separator];
    }

    private static MemoryActor? ParseActor(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var separator = candidate.IndexOf('+', StringComparison.Ordinal);
        var alias = separator < 0 ? candidate : candidate[..separator];
        if (string.IsNullOrWhiteSpace(alias))
        {
            return null;
        }

        var vendor = separator < 0 ? null : candidate[(separator + 1)..];
        return new MemoryActor(alias, string.IsNullOrWhiteSpace(vendor) ? null : vendor);
    }
}
