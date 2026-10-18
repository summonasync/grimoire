namespace Grimoire.Cli.CommandLine;

internal sealed record CommandSpec
{
    public required string Name { get; init; }

    public required string Summary { get; init; }

    public required string Syntax { get; init; }

    public int MinPositional { get; init; }

    public IReadOnlyList<FlagSpec> Flags { get; init; } = [];

    public FlagSpec? FindFlag(string name) =>
        Flags.FirstOrDefault(flag => flag.Name.Equals(name, StringComparison.Ordinal));
}
