using Grimoire.Cli.CommandLine;
using Grimoire.Core;

namespace Grimoire.Cli;

internal static class BriefHookRenderer
{
    public const string NotWiredMessage =
        "--format hook is not wired in this build yet; the brief prints text. Drop --format or pass --format text.";

    public static void Write(CommandSpec spec, CliEnvironment environment, BriefResult brief, bool daily) =>
        throw new UsageException(NotWiredMessage, spec);
}
