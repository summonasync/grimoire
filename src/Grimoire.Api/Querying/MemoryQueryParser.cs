using Grimoire.Core;

namespace Grimoire.Api.Querying;

public static class MemoryQueryParser
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 500;

    private static readonly IReadOnlyList<MemoryStatus> AllStatuses =
        [MemoryStatus.Active, MemoryStatus.Superseded, MemoryStatus.Tombstoned];

    public static ParsedMemoryQuery Parse(MemoryListQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var range = ParseRange(query.Since, query.Until);

        return new ParsedMemoryQuery(
            ParseStatuses(query.Status),
            new MemoryFilter
            {
                Scopes = ParseScopes(query.Scope),
                Kind = ParseKind(query.Kind),
                Tags = SplitList(query.Tag),
                ProjectId = query.ProjectId,
                SessionId = query.SessionId,
                Tome = ParseTome(query.Tome),
                CreatedAfter = range.Since,
                CreatedBefore = range.Until,
                Actor = query.Actor,
            },
            Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit),
            FieldsParam.Resolve(query.Fields, MemoryRecordProjection.Surface));
    }

    private static CreatedAtRange ParseRange(string? since, string? until)
    {
        try
        {
            return CreatedAtRange.Parse(since, until);
        }
        catch (ArgumentException failure)
        {
            throw new MemoryQueryException($"invalid_{failure.ParamName}", failure.Message);
        }
    }

    private static IReadOnlyList<MemoryStatus> ParseStatuses(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [MemoryStatus.Active];
        }

        var trimmed = raw.Trim();
        if (trimmed.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return AllStatuses;
        }

        var requested = new HashSet<MemoryStatus>();
        foreach (var part in SplitList(trimmed))
        {
            if (!Enum.TryParse<MemoryStatus>(part, ignoreCase: true, out var status) || !Enum.IsDefined(status))
            {
                throw new MemoryQueryException(
                    "invalid_status",
                    $"Unknown memory status '{part}'. Use active, superseded, tombstoned or all.");
            }

            requested.Add(status);
        }

        return [.. AllStatuses.Where(requested.Contains)];
    }

    private static IReadOnlyList<MemoryScope> ParseScopes(string? raw)
    {
        var scopes = new List<MemoryScope>();
        foreach (var part in SplitList(raw))
        {
            if (!Enum.TryParse<MemoryScope>(part, ignoreCase: true, out var scope) || !Enum.IsDefined(scope))
            {
                throw new MemoryQueryException(
                    "invalid_scope",
                    $"Unknown memory scope '{part}'. Use session, project, global or personal.");
            }

            scopes.Add(scope);
        }

        return scopes;
    }

    private static MemoryKind? ParseKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!Enum.TryParse<MemoryKind>(raw, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            throw new MemoryQueryException("invalid_kind", $"Unknown memory kind '{raw}'. Use journal or fact.");
        }

        return kind;
    }

    private static string? ParseTome(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!TomeKey.TryNormalize(raw, out var tome))
        {
            throw new MemoryQueryException(
                "invalid_tome",
                $"Tome must be a lowercase kebab-case tome key; '{raw}' is not a valid tome key.");
        }

        return tome;
    }

    private static string[] SplitList(string? raw) => string.IsNullOrWhiteSpace(raw)
        ? []
        : raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
