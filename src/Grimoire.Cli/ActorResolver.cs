using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Mcp;

namespace Grimoire.Cli;

internal static class ActorResolver
{
    public const string EnvironmentVariable = "GRIMOIRE_ACTOR";

    public static MemoryActor Resolve(CliEnvironment environment, string? flag) =>
        TryResolve(environment, flag) ?? MemoryActor.Human;

    public static MemoryActor? TryResolve(CliEnvironment environment, string? flag)
    {
        var text = flag ?? environment.Variable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return Parse(text);
    }

    public static async Task<CliIdentity> ResolveAsync(
        CliEnvironment environment,
        string? actorFlag,
        string? tomeFlag,
        string defaultTome,
        IActorClaimProvider claims,
        CancellationToken cancellationToken)
    {
        var claim = tomeFlag is null ? await claims.ReadAsync(cancellationToken) : null;
        var envActor = environment.Variable(EnvironmentVariable);

        var source = TomeSource.Default;
        string tome;
        if (tomeFlag is { } explicitTome)
        {
            tome = explicitTome;
            source = TomeSource.ExplicitParam;
        }
        else if (environment.Variable(McpHostOptions.TomeEnvironmentVariable) is { } envTome)
        {
            tome = envTome;
            source = TomeSource.EnvTome;
        }
        else if (AliasPart(envActor) is { } envActorAlias)
        {
            tome = envActorAlias;
            source = TomeSource.EnvActor;
        }
        else if (claim?.Tome is { } claimTome)
        {
            tome = claimTome;
            source = TomeSource.ClaimFile;
        }
        else if (claim?.Alias is { } claimAlias)
        {
            tome = claimAlias;
            source = TomeSource.ClaimFile;
        }
        else
        {
            tome = defaultTome;
        }

        var actor = Parse(actorFlag)
            ?? Parse(envActor)
            ?? (claim?.Alias is { } alias ? new MemoryActor?(new MemoryActor(alias, null)) : null)
            ?? MemoryActor.Human;

        return new CliIdentity
        {
            Actor = actor,
            Tome = source == TomeSource.Default ? TomeKey.Normalize(tome) : Normalized(tome, source),
            Source = source,
        };
    }

    private static MemoryActor? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var separator = text.IndexOf('+', StringComparison.Ordinal);
        var alias = separator < 0 ? text : text[..separator];
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new UsageException(
                $"--actor and {EnvironmentVariable} expect '<alias>' or '<alias>+<vendor>', got '{text}'.");
        }

        var vendor = separator < 0 ? null : text[(separator + 1)..];
        return new MemoryActor(alias, string.IsNullOrWhiteSpace(vendor) ? null : vendor);
    }

    private static string Normalized(string candidate, TomeSource source) =>
        TomeKey.TryNormalize(candidate, out var tome)
            ? tome
            : throw new UsageException(
                $"{Origin(source)} must be a lowercase kebab-case tome key; '{candidate}' is not a valid tome key.");

    private static string Origin(TomeSource source) => source switch
    {
        TomeSource.ExplicitParam => "--tome",
        TomeSource.EnvTome => McpHostOptions.TomeEnvironmentVariable,
        TomeSource.EnvActor => EnvironmentVariable,
        TomeSource.ClaimFile => $"The claim block in {ActorClaimProvider.FileName}",
        _ => "The tome candidate",
    };

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
