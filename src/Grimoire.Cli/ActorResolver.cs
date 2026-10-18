using Grimoire.Cli.CommandLine;
using Grimoire.Core;

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
}
