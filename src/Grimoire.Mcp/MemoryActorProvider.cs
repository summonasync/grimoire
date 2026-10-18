using Grimoire.Core;

namespace Grimoire.Mcp;

internal sealed class MemoryActorProvider(
    MemoryActor? configured,
    string? envTome,
    string? envActor,
    ClientInfoHolder clientInfo,
    IActorClaimProvider claims,
    TomeResolver tomes)
{
    private static readonly MemoryActor UnknownActor = new("unknown", null);

    public MemoryActor GetActor()
    {
        if (configured.HasValue)
        {
            return configured.Value;
        }

        var clientName = clientInfo.Read()?.Name;
        return string.IsNullOrWhiteSpace(clientName) ? UnknownActor : new MemoryActor(clientName, null);
    }

    public async Task<string> ResolveTomeAsync(string? explicitTome, CancellationToken cancellationToken)
    {
        var claim = explicitTome is null ? await claims.ReadAsync(cancellationToken) : null;
        return tomes.Resolve(explicitTome, envTome, AliasPart(envActor), claim, clientInfo.Read()?.Name).ResolvedTome;
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
}
