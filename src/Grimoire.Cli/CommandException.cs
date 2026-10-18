namespace Grimoire.Cli;

internal class CommandException(string code, string message, int exitCode = ExitCodes.Runtime) : Exception(message)
{
    public string Code { get; } = code;

    public int ExitCode { get; } = exitCode;
}
