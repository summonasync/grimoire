namespace Grimoire.Cli.CommandLine;

internal sealed record CliInvocation
{
    public required CommandSpec Spec { get; init; }

    public IReadOnlyList<string> Positional { get; init; } = [];

    public IReadOnlyDictionary<string, string> Options { get; init; } = new Dictionary<string, string>();

    public string? Value(string name) => Options.TryGetValue(name, out var value) ? value : null;

    public string JoinedPositional() => string.Join(" ", Positional);
}
