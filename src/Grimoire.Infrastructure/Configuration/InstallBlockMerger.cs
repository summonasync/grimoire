namespace Grimoire.Infrastructure.Configuration;

public static class InstallBlockMerger
{
    public static string Merge(string existing, string block)
    {
        if (existing.Length == 0)
        {
            return block + "\n";
        }

        var start = existing.IndexOf(InstallBlock.BeginMarker, StringComparison.Ordinal);
        if (start >= 0)
        {
            var end = existing.IndexOf(InstallBlock.EndMarker, start + InstallBlock.BeginMarker.Length, StringComparison.Ordinal);
            if (end >= 0)
            {
                return existing[..start] + block + existing[(end + InstallBlock.EndMarker.Length)..];
            }
        }

        var separator = existing.EndsWith("\n\n", StringComparison.Ordinal)
            ? string.Empty
            : existing.EndsWith('\n')
                ? "\n"
                : "\n\n";

        return $"{existing}{separator}{block}\n";
    }

    public static string Strip(string content)
    {
        var start = content.IndexOf(InstallBlock.BeginMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            return content;
        }

        var end = content.IndexOf(InstallBlock.EndMarker, start + InstallBlock.BeginMarker.Length, StringComparison.Ordinal);
        if (end < 0)
        {
            return content;
        }

        var prefix = content[..start];
        if (prefix.EndsWith("\n\n", StringComparison.Ordinal))
        {
            prefix = prefix[..^1];
        }

        var suffix = content[(end + InstallBlock.EndMarker.Length)..];
        if (suffix.StartsWith('\n'))
        {
            suffix = suffix[1..];
        }

        return prefix + suffix;
    }
}
