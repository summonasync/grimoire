namespace Grimoire.Core;

public static class RecordLinkParser
{
    public static IReadOnlyList<Guid> Parse(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        var ids = new List<Guid>();
        var cursor = 0;

        while (true)
        {
            var open = content.IndexOf("[[", cursor, StringComparison.Ordinal);
            if (open < 0)
            {
                break;
            }

            var close = content.IndexOf("]]", open + 2, StringComparison.Ordinal);
            if (close < 0)
            {
                break;
            }

            if (Guid.TryParse(content.AsSpan(open + 2, close - open - 2), out var id))
            {
                ids.Add(id);
            }

            cursor = close + 2;
        }

        return ids;
    }

    public static IReadOnlyList<Guid> ParseReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return [];
        }

        var ids = new List<Guid>();
        var start = -1;

        for (var i = 0; i <= reason.Length; i++)
        {
            var isIdChar = i < reason.Length && IsIdChar(reason[i]);

            if (isIdChar && start < 0)
            {
                start = i;
            }
            else if (!isIdChar && start >= 0)
            {
                if (Guid.TryParse(reason.AsSpan(start, i - start), out var id))
                {
                    ids.Add(id);
                }

                start = -1;
            }
        }

        return ids;
    }

    private static bool IsIdChar(char character) =>
        character is '-' or (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
}
