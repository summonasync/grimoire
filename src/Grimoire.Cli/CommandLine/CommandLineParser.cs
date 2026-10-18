namespace Grimoire.Cli.CommandLine;

internal static class CommandLineParser
{
    public static CliInvocation Parse(string[] args)
    {
        if (args.Length == 0)
        {
            throw new UsageException("No command given. Run 'grimoire help' to list commands.");
        }

        var spec = CommandCatalog.Find(args[0])
            ?? throw new UsageException($"Unknown command '{args[0]}'. Run 'grimoire help' to list commands.");

        var positional = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var index = 1; index < args.Length; index++)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(token);
                continue;
            }

            var (name, inlineValue) = SplitFlag(token[2..]);
            var flag = spec.FindFlag(name)
                ?? throw new UsageException($"Unknown option '{name}' for '{spec.Name}'. Run 'grimoire help {spec.Name}'.", spec);

            string value;
            if (inlineValue is not null)
            {
                value = inlineValue;
            }
            else if (index + 1 < args.Length)
            {
                value = args[++index];
            }
            else
            {
                throw new UsageException($"Option '{name}' requires a value.", spec);
            }

            options[flag.Name] = value;
        }

        if (positional.Count < spec.MinPositional)
        {
            throw new UsageException($"'{spec.Name}' is missing arguments. Usage: grimoire {spec.Syntax}", spec);
        }

        return new CliInvocation { Spec = spec, Positional = positional, Options = options };
    }

    private static (string Name, string? InlineValue) SplitFlag(string token)
    {
        var separator = token.IndexOf('=', StringComparison.Ordinal);
        return separator < 0
            ? (token, null)
            : (token[..separator], token[(separator + 1)..]);
    }
}
