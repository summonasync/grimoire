namespace Grimoire.Core;

public static class AutoMemoryFileParser
{
    private const string MemoryIndexFile = "memory.md";

    public static async Task<IReadOnlyList<AutoMemoryCandidate>> ParseAsync(string directory, CancellationToken cancellationToken)
    {
        var candidates = new List<AutoMemoryCandidate>();

        foreach (var path in Directory
                     .EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly)
                     .Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(path);
            var body = StripFrontmatter(await File.ReadAllTextAsync(path, cancellationToken));

            if (fileName.Equals(MemoryIndexFile, StringComparison.OrdinalIgnoreCase))
            {
                if (!CarriesContent(body))
                {
                    continue;
                }
            }
            else if (string.IsNullOrWhiteSpace(body))
            {
                continue;
            }

            candidates.Add(new AutoMemoryCandidate
            {
                FileName = fileName,
                Content = body,
                ModifiedAt = File.GetLastWriteTimeUtc(path),
            });
        }

        return candidates;
    }

    private static string StripFrontmatter(string text)
    {
        var reader = new StringReader(text);
        if (reader.ReadLine() is not "---")
        {
            return text;
        }

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line is "---" or "...")
            {
                return reader.ReadToEnd();
            }
        }

        return text;
    }

    private static bool CarriesContent(string body) => body
        .Split('\n')
        .Select(line => line.Trim())
        .Any(line => line.Length > 0 && line[0] != '#' && !IsIndexLink(line));

    private static bool IsIndexLink(string line)
    {
        var rest = line.Length >= 2 && line[1] == ' ' && line[0] is '-' or '*' or '+'
            ? line[2..]
            : OrderedListItemRest(line);

        return rest is not null && rest.StartsWith('[');
    }

    private static string? OrderedListItemRest(string line)
    {
        var digits = 0;
        while (digits < line.Length && char.IsAsciiDigit(line[digits]))
        {
            digits++;
        }

        return digits > 0 && digits < line.Length - 1 && line[digits] is '.' or ')' && line[digits + 1] == ' '
            ? line[(digits + 2)..]
            : null;
    }
}
