using System.Diagnostics.CodeAnalysis;

namespace Grimoire.Core;

public sealed class TomeResolver(MemoryEngineOptions? options = null)
{
    private readonly MemoryEngineOptions _options = options ?? new();

    public TomeResolution Resolve(
        string? explicitTome = null,
        string? envTome = null,
        string? envActor = null,
        ActorClaim? claim = null,
        string? clientActor = null)
    {
        if (Present(explicitTome, out var tome))
        {
            return Resolved(tome, TomeSource.ExplicitParam);
        }

        if (Present(envTome, out tome))
        {
            return Resolved(tome, TomeSource.EnvTome);
        }

        if (Present(envActor, out tome))
        {
            return Resolved(tome, TomeSource.EnvActor);
        }

        if (Present(claim?.Tome, out tome) || Present(claim?.Alias, out tome))
        {
            return Resolved(tome, TomeSource.ClaimFile);
        }

        if (Present(clientActor, out tome))
        {
            return Resolved(tome, TomeSource.ClientInfo);
        }

        return Resolved(_options.DefaultTome, TomeSource.Default);
    }

    private static bool Present(string? candidate, [NotNullWhen(true)] out string? value)
    {
        value = string.IsNullOrWhiteSpace(candidate) ? null : candidate;
        return value is not null;
    }

    private TomeResolution Resolved(string candidate, TomeSource source) =>
        new() { ResolvedTome = TomeKey.Normalize(candidate), Source = source };
}
