namespace Grimoire.Core;

public static class LexicalTermMatcher
{
    public static IReadOnlyList<string> Tokenize(string query)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var tokens = new List<string>();
        var start = -1;

        for (var i = 0; i <= query.Length; i++)
        {
            var inWord = i < query.Length && char.IsLetterOrDigit(query[i]);
            if (inWord && start < 0)
            {
                start = i;
            }
            else if (!inWord && start >= 0)
            {
                var token = query[start..i].ToLowerInvariant();
                if (seen.Add(token))
                {
                    tokens.Add(token);
                }

                start = -1;
            }
        }

        return tokens;
    }

    public static IReadOnlyList<TermSpan> Match(IReadOnlyList<string> terms, MemoryRecord record)
    {
        var spans = new List<TermSpan>();

        foreach (var term in terms)
        {
            var inContent = record.Content.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (inContent >= 0)
            {
                spans.Add(new TermSpan(term, "content", inContent));
                continue;
            }

            foreach (var tag in record.Tags)
            {
                var inTag = tag.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                if (inTag < 0)
                {
                    continue;
                }

                spans.Add(new TermSpan(term, "tag", inTag));
                break;
            }
        }

        return spans;
    }
}
