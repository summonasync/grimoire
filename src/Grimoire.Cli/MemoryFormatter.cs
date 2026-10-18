using Grimoire.Core;

namespace Grimoire.Cli;

internal static class MemoryFormatter
{
    public static void WriteRecord(TextWriter output, MemoryRecord record)
    {
        output.WriteLine($"{record.Id} {Describe(record)}");
        WriteContent(output, record.Content);
    }

    public static void WriteMatch(TextWriter output, RecallMatch match)
    {
        output.WriteLine($"{match.Score:F3} {match.Record.Id} {Describe(match.Record)}");
        WriteContent(output, match.Record.Content);
    }

    private static void WriteContent(TextWriter output, string content)
    {
        foreach (var line in content.Split('\n'))
        {
            output.WriteLine($"  {line.TrimEnd('\r')}");
        }
    }

    private static string Describe(MemoryRecord record)
    {
        var details = $"{record.Scope}/{record.Kind} {record.Status.ToString().ToLowerInvariant()} {record.CreatedAt:u} by {record.Actor}";
        var tags = string.Join(",", record.Tags);
        return tags.Length == 0 ? details : $"{details} [{tags}]";
    }
}
