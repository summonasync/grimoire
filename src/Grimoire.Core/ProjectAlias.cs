using System.Diagnostics.CodeAnalysis;

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

    private static bool IsAliasCharacter(char character) =>
        character is '-' or (>= '0' and <= '9') or (>= 'a' and <= 'z');
}
