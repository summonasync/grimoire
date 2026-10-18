using System.Globalization;
using Grimoire.Cli.CommandLine;
using Grimoire.Core;

namespace Grimoire.Cli;

internal static class CliValues
{
    public static MemoryScope Scope(CommandSpec spec, string? value, MemoryScope fallback) =>
        value is null ? fallback : ParseScope(spec, value);

    public static IReadOnlyList<MemoryScope> ScopeList(CommandSpec spec, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var scopes = new List<MemoryScope>();
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            scopes.Add(ParseScope(spec, part));
        }

        return scopes;
    }

    public static MemoryKind Kind(CommandSpec spec, string? value, MemoryKind fallback) =>
        value is null ? fallback
            : Enum.TryParse<MemoryKind>(value, ignoreCase: true, out var kind) && Enum.IsDefined(kind) ? kind
            : throw new UsageException($"Unknown kind '{value}' (expected: journal, fact).", spec);

    public static MemoryStatus Status(CommandSpec spec, string? value, MemoryStatus fallback) =>
        value is null ? fallback
            : Enum.TryParse<MemoryStatus>(value, ignoreCase: true, out var status) && Enum.IsDefined(status) ? status
            : throw new UsageException($"Unknown status '{value}' (expected: active, superseded, tombstoned).", spec);

    public static PersonaSection Section(CommandSpec spec, string value) =>
        Enum.TryParse<PersonaSection>(value, ignoreCase: true, out var section) && Enum.IsDefined(section) ? section
            : throw new UsageException(
                $"Unknown persona section '{value}' (expected: identity, tone, preferences, rules).", spec);

    public static IReadOnlyList<string> Tags(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static double Unit(CommandSpec spec, string name, string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || number < 0 || number > 1)
        {
            throw new UsageException($"--{name} must be a number between 0 and 1, got '{value}'.", spec);
        }

        return number;
    }

    public static double Hours(CommandSpec spec, string name, string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var hours) || hours <= 0)
        {
            throw new UsageException($"--{name} must be a positive number of hours, got '{value}'.", spec);
        }

        return hours;
    }

    public static int PositiveInt(CommandSpec spec, string name, string value)
    {
        if (!int.TryParse(value, out var number) || number <= 0)
        {
            throw new UsageException($"--{name} must be a positive integer, got '{value}'.", spec);
        }

        return number;
    }

    private static MemoryScope ParseScope(CommandSpec spec, string value) =>
        Enum.TryParse<MemoryScope>(value, ignoreCase: true, out var scope) && Enum.IsDefined(scope) ? scope
            : throw new UsageException($"Unknown scope '{value}' (expected: session, project, global, personal).", spec);
}
