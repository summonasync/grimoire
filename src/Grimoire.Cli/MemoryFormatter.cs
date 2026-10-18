using Grimoire.Core;

namespace Grimoire.Cli;

internal static class MemoryFormatter
{
    public static void WriteRecord(TextWriter output, MemoryRecord record)
    {
        output.WriteLine($"{record.Id} {Describe(record)}");
        WriteLines(output, record.Content, "  ");
    }

    public static void WriteMatch(TextWriter output, RecallMatch match)
    {
        output.WriteLine($"{match.Score:F3} {match.Record.Id} {Describe(match.Record)}");
        WriteLines(output, match.Record.Content, "  ");
    }

    public static void WriteBrief(TextWriter output, BriefResult brief)
    {
        output.WriteLine(Heading("persona", brief.PersonaState));
        if (brief.Persona is { } persona)
        {
            foreach (var section in PersonaSectionExtensions.Sections)
            {
                output.WriteLine($"  {section.ToString().ToLowerInvariant()}:");
                WriteLines(output, persona.Read(section), "    ");
            }
        }

        output.WriteLine(Heading("facts", brief.FactsState));
        foreach (var fact in brief.Facts)
        {
            output.WriteLine($"  {fact.Score:F3} {fact.Id} {fact.Scope} {fact.CreatedAt:u}");
            WriteLines(output, fact.Content, "    ");
        }

        output.WriteLine(Heading("tombstones", brief.TombstonesState));
        foreach (var tombstone in brief.Tombstones)
        {
            output.WriteLine($"  {tombstone.Id} {tombstone.TombstonedAt:u}");
            WriteLines(output, tombstone.Reason ?? "(no reason)", "    ");
        }

        output.WriteLine(Heading("journal", brief.JournalState));
        foreach (var entry in brief.Journal)
        {
            output.WriteLine($"  {entry.Id} {entry.Scope} {entry.CreatedAt:u}");
            WriteLines(output, entry.Content, "    ");
            if (entry.Truncated)
            {
                output.WriteLine("    (truncated)");
            }
        }
    }

    private static string Heading(string name, BriefSectionState state) => state switch
    {
        BriefSectionState.Empty => $"{name}: (empty)",
        BriefSectionState.Omitted => $"{name}: (omitted: over budget)",
        _ => $"{name}:",
    };

    private static void WriteLines(TextWriter output, string content, string indent)
    {
        if (content.Length == 0)
        {
            output.WriteLine($"{indent}(empty)");
            return;
        }

        foreach (var line in content.Split('\n'))
        {
            output.WriteLine($"{indent}{line.TrimEnd('\r')}");
        }
    }

    private static string Describe(MemoryRecord record)
    {
        var details = $"{record.Scope}/{record.Kind} {record.Status.ToString().ToLowerInvariant()} {record.CreatedAt:u} by {record.Actor}";
        var tags = string.Join(",", record.Tags);
        return tags.Length == 0 ? details : $"{details} [{tags}]";
    }
}
