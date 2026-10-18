using Grimoire.Cli.CommandLine;

namespace Grimoire.Cli;

internal sealed class UsageException(string message, CommandSpec? spec = null) : CommandException("usage", message, ExitCodes.Usage)
{
    public CommandSpec? Spec { get; } = spec;
}
