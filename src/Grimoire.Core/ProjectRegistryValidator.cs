namespace Grimoire.Core;

public static class ProjectRegistryValidator
{
    public static IReadOnlyList<string> Validate(IEnumerable<ProjectAliasRecord>? records)
    {
        var errors = new List<string>();
        if (records is null)
        {
            return errors;
        }

        var materialized = records as IList<ProjectAliasRecord> ?? [.. records];
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        var boardIds = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < materialized.Count; index++)
        {
            ValidateRecord(materialized[index], index, errors, aliases, boardIds, paths);
        }

        return errors;
    }

    internal static string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(path.Trim());

    private static void ValidateRecord(
        ProjectAliasRecord record,
        int index,
        ICollection<string> errors,
        ISet<string> aliases,
        ISet<string> boardIds,
        ISet<string> paths)
    {
        var label = $"projects[{index}]";

        if (string.IsNullOrWhiteSpace(record.Alias))
        {
            errors.Add($"{label}: alias must not be empty.");
        }
        else if (!ProjectAlias.IsValid(record.Alias))
        {
            errors.Add(
                $"{label}: alias '{record.Alias}' must be lowercase kebab-case (a-z, 0-9, single dashes, at most {ProjectAlias.MaxLength} characters).");
        }
        else if (!aliases.Add(record.Alias))
        {
            errors.Add($"{label}: alias '{record.Alias}' is already registered.");
        }

        if (record.BoardProjectId is { } boardId && !boardIds.Add(boardId))
        {
            errors.Add($"{label}: boardProjectId '{boardId}' is already registered.");
        }

        if (record.Paths is null)
        {
            errors.Add($"{label}: paths must not be null.");
            return;
        }

        foreach (var path in record.Paths)
        {
            ValidatePath(path, label, errors, paths);
        }
    }

    private static void ValidatePath(string? path, string label, ICollection<string> errors, ISet<string> paths)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            errors.Add($"{label}: paths must not contain empty entries.");
            return;
        }

        if (!Path.IsPathRooted(path))
        {
            errors.Add($"{label}: path '{path}' must be absolute.");
            return;
        }

        if (!paths.Add(NormalizePath(path)))
        {
            errors.Add($"{label}: path '{path}' is already registered.");
        }
    }
}
