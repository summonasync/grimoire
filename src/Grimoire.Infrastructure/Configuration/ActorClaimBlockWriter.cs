namespace Grimoire.Infrastructure.Configuration;

public static class ActorClaimBlockWriter
{
    public static string Upsert(string document, string alias, string? tome)
    {
        var block = Render(alias, tome);
        var start = document.IndexOf(ActorClaimBlockParser.StartMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            return Append(document, block);
        }

        var contentStart = start + ActorClaimBlockParser.StartMarker.Length;
        var end = document.IndexOf(ActorClaimBlockParser.EndMarker, contentStart, StringComparison.Ordinal);
        var tail = end < 0 ? string.Empty : document[(end + ActorClaimBlockParser.EndMarker.Length)..];
        return $"{document[..start]}{block}{tail}";
    }

    public static string Render(string alias, string? tome)
    {
        var lines = new List<string>
        {
            ActorClaimBlockParser.StartMarker,
            $"alias: {alias}",
        };

        if (!string.IsNullOrWhiteSpace(tome))
        {
            lines.Add($"tome: {tome}");
        }

        lines.Add(ActorClaimBlockParser.EndMarker);
        return string.Join(Environment.NewLine, lines);
    }

    private static string Append(string document, string block)
    {
        if (document.Length == 0)
        {
            return block + Environment.NewLine;
        }

        var separator = document.EndsWith('\n') ? Environment.NewLine : Environment.NewLine + Environment.NewLine;
        return $"{document}{separator}{block}{Environment.NewLine}";
    }
}
