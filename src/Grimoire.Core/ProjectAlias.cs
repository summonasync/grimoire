using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Grimoire.Core;

public static class ProjectAlias
{
    public const int MaxLength = 64;

    public static bool IsValid([NotNullWhen(true)] string? candidate)
    {
        if (string.IsNullOrEmpty(candidate) || candidate.Length > MaxLength)
        {
            return false;
        }

        return candidate[0] != '-'
            && candidate[^1] != '-'
            && !candidate.Contains("--")
            && candidate.All(IsAliasCharacter);
    }

    public static string Propose(string source, IEnumerable<string> taken)
    {
        var existing = new HashSet<string>(taken, StringComparer.Ordinal);
        var stem = Stem(source);
        if (!existing.Contains(stem))
        {
            return stem;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{stem}-{suffix}";
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static string Stem(string source)
    {
        var builder = new StringBuilder();
        foreach (var character in source.ToLowerInvariant())
        {
            if (character is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var stem = builder.ToString().TrimEnd('-');
        if (stem.Length > MaxLength - 4)
        {
            stem = stem[..(MaxLength - 4)].TrimEnd('-');
        }

        return stem.Length == 0 ? "project" : stem;
    }

    private static bool IsAliasCharacter(char character) =>
        character is '-' or (>= '0' and <= '9') or (>= 'a' and <= 'z');
}
