namespace Grimoire.Cli.CommandLine;

internal static class UsageWriter
{
    public static void WriteGeneral(TextWriter output)
    {
        output.WriteLine("Grimoire - local agent memory service.");
        output.WriteLine();
        output.WriteLine("Usage: grimoire <command> [arguments] [--option value]");
        output.WriteLine();
        output.WriteLine("Commands:");
        foreach (var spec in CommandCatalog.All)
        {
            output.WriteLine($"  {spec.Name.PadRight(10)}{spec.Summary}");
        }

        output.WriteLine();
        output.WriteLine("Run 'grimoire help <command>' for command details. Config: ~/.grimoire/config.json");
    }

    public static void WriteCommand(TextWriter output, CommandSpec spec)
    {
        output.WriteLine($"Usage: grimoire {spec.Syntax}");
        output.WriteLine();
        output.WriteLine(spec.Summary);
        if (spec.Flags.Count == 0)
        {
            return;
        }

        var left = spec.Flags.Select(flag => $"  --{flag.Name} <{flag.ValueName}>").ToList();
        var width = left.Max(text => text.Length);
        output.WriteLine();
        output.WriteLine("Options:");
        for (var index = 0; index < spec.Flags.Count; index++)
        {
            output.WriteLine($"{left[index].PadRight(width + 2)}{spec.Flags[index].Description}");
        }
    }

    public static void WriteRecovery(TextWriter output, CommandSpec? spec)
    {
        if (spec is null)
        {
            WriteGeneral(output);
        }
        else
        {
            WriteCommand(output, spec);
        }
    }
}
