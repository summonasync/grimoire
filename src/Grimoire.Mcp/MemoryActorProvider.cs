using Grimoire.Core;

namespace Grimoire.Mcp;

internal sealed class MemoryActorProvider(
    MemoryActor? configured,
    string? envTome,
    string? envActor,
    SessionIdentityHolder sessionIdentity,
    IAgentIdentitySource identityFile,
    ClientInfoHolder clientInfo,
    IActorClaimProvider claims,
    TomeResolver tomes)
{
    private static readonly MemoryActor UnknownActor = new("unknown", null);

    public async Task<MemoryActor> GetActorAsync(CancellationToken cancellationToken)
    {
        if (configured.HasValue)
        {
            return configured.Value;
        }

        if (sessionIdentity.Read() is { } sessionActor)
        {
            return sessionActor;
        }

        if (identityFile.Get() is { } fileActor)
        {
            return fileActor;
        }

        return ActorPart(envActor)
            ?? await ClaimActorAsync(cancellationToken)
            ?? ClientActor()
            ?? UnknownActor;
    }

    public async Task<string> ResolveTomeAsync(string? explicitTome, CancellationToken cancellationToken)
    {
        var claim = explicitTome is null ? await claims.ReadAsync(cancellationToken) : null;
        return tomes.Resolve(explicitTome, envTome, ActorPart(envActor)?.Alias, claim, clientInfo.Read()?.Name).ResolvedTome;
    }

    private async Task<MemoryActor?> ClaimActorAsync(CancellationToken cancellationToken)
    {
        var claim = await claims.ReadAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(claim?.Alias)
            ? null
            : new MemoryActor(claim!.Alias, string.IsNullOrWhiteSpace(claim.Vendor) ? null : claim.Vendor);
    }

    private MemoryActor? ClientActor()
    {
        var clientName = clientInfo.Read()?.Name;
        return string.IsNullOrWhiteSpace(clientName) ? null : new MemoryActor(clientName, null);
    }

    private static MemoryActor? ActorPart(string? candidate)
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
