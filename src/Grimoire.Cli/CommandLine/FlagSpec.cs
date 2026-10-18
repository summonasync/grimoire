namespace Grimoire.Cli.CommandLine;

internal sealed record FlagSpec(string Name, string ValueName, string Description, bool IsSwitch = false)
{
    public static FlagSpec Switch(string name, string description) => new(name, "", description, IsSwitch: true);
}
