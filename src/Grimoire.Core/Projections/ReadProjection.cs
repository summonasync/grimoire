namespace Grimoire.Core;

public static class ReadProjection
{
    public static IReadOnlyList<string>? Resolve(IReadOnlyList<string>? fields, bool compact, ReadProjectionSurface surface)
    {
        if (fields is not { Count: > 0 })
        {
            return compact ? Merge(surface.CompactKeys, surface.AlwaysOnKeys) : null;
        }

        if (compact)
        {
            throw new ReadProjectionValidationException("fields and compact are mutually exclusive; pass one of them.");
        }

        var allowed = new HashSet<string>(surface.AllowedKeys, StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (!allowed.Contains(field))
            {
                throw new ReadProjectionValidationException(
                    $"Unknown field '{field}'. Allowed fields: {string.Join(", ", surface.AllowedKeys)}.");
            }
        }

        return Merge(fields, surface.AlwaysOnKeys);
    }

    private static IReadOnlyList<string> Merge(IReadOnlyList<string> requested, IReadOnlyList<string> alwaysOn)
    {
        var keys = new List<string>(alwaysOn.Count + requested.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var key in alwaysOn.Concat(requested))
        {
            if (seen.Add(key))
            {
                keys.Add(key);
            }
        }

        return keys;
    }
}
