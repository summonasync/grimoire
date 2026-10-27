using Grimoire.Api.Contracts;
using Grimoire.Core;

namespace Grimoire.Api.Querying;

public static class ScryQueryParser
{
    public const int DefaultLimit = 5;

    public static ParsedScryQuery Parse(ScryQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Q))
        {
            throw new MemoryQueryException("invalid_query", "A scry query is required.");
        }

        var tomes = new List<string>();
        foreach (var candidate in SplitList(query.Tomes))
        {
            if (!TomeKey.TryNormalize(candidate, out var tome))
            {
                throw new MemoryQueryException(
                    "invalid_tome",
                    $"Each entry of tomes must be a lowercase kebab-case tome key; '{candidate}' is not a valid tome key.");
            }

            tomes.Add(tome);
        }

        var attribution = ParseAttribution(query.Attribution);

        if (query.Limit is <= 0)
        {
            throw new MemoryQueryException("invalid_limit", "limit must be a positive integer.");
        }

        return new ParsedScryQuery(
            query.Q.Trim(),
            [.. tomes.Distinct(StringComparer.Ordinal).OrderBy(tome => tome, StringComparer.Ordinal)],
            query.IncludeJournal ?? false,
            attribution,
            query.Limit ?? DefaultLimit,
            FieldsParam.Resolve(query.Fields, ScryItemProjection.Surface));
    }

    private static ScryAttribution ParseAttribution(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ScryAttribution.None;
        }

        if (!Enum.TryParse<ScryAttribution>(raw, ignoreCase: true, out var attribution) || !Enum.IsDefined(attribution))
        {
            throw new MemoryQueryException(
                "invalid_attribution",
                $"Unknown attribution '{raw}'. Use none, light or full.");
        }

        return attribution;
    }

    private static string[] SplitList(string? raw) => string.IsNullOrWhiteSpace(raw)
        ? []
        : raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
