using Grimoire.Core;

namespace Grimoire.Infrastructure.Configuration;

public static class ActorClaimBlockParser
{
    public const string StartMarker = "<!-- grimoire:actor:start -->";

    public const string EndMarker = "<!-- grimoire:actor:end -->";

    public static ActorClaim? Parse(string document)
    {
        var start = document.IndexOf(StartMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        var contentStart = start + StartMarker.Length;
        var end = document.IndexOf(EndMarker, contentStart, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        string? alias = null;
        string? tome = null;
        foreach (var rawLine in document[contentStart..end].Split('\n'))
        {
            var line = rawLine.Trim();
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (key.Equals("alias", StringComparison.OrdinalIgnoreCase))
            {
                alias = Keep(alias, value);
            }
            else if (key.Equals("tome", StringComparison.OrdinalIgnoreCase))
            {
                tome = Keep(tome, value);
            }
        }

        return alias is null && tome is null ? null : new ActorClaim { Alias = alias, Tome = tome };
    }

    private static string? Keep(string? current, string value) =>
        string.IsNullOrWhiteSpace(value) ? current : value;
}
