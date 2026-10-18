using System.Diagnostics.CodeAnalysis;

namespace Grimoire.Core;

public static class TomeKey
{
    public const string DefaultTome = "default";

    public const int MaxLength = 64;

    public static bool TryNormalize(string? candidate, [NotNullWhen(true)] out string? normalized)
    {
        try
        {
            normalized = Normalize(candidate);
            return true;
        }
        catch (ArgumentException)
        {
            normalized = null;
            return false;
        }
    }

    public static string Normalize(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new ArgumentException("A tome key must not be empty.", nameof(candidate));
        }

        var normalized = candidate.Trim()
            .ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('_', '-');

        while (normalized.Contains("--"))
        {
            normalized = normalized.Replace("--", "-");
        }

        normalized = normalized.Trim('-');

        if (normalized.Length is 0 or > MaxLength)
        {
            throw new ArgumentException(
                $"A tome key must be between 1 and {MaxLength} characters long.", nameof(candidate));
        }

        foreach (var character in normalized)
        {
            if (character is not ('-' or (>= '0' and <= '9') or (>= 'a' and <= 'z')))
            {
                throw new ArgumentException(
                    $"A tome key must be lowercase kebab-case; '{candidate}' is not a valid tome key.", nameof(candidate));
            }
        }

        return normalized;
    }
}
