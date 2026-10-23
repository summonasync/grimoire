namespace Grimoire.Core;

public sealed class ProjectRegistry : IProjectRegistry
{
    private readonly IReadOnlyList<ProjectAliasRecord> _records;
    private readonly IReadOnlyList<(string Alias, string Prefix)> _pathPrefixes;

    public ProjectRegistry(IEnumerable<ProjectAliasRecord> records)
    {
        List<ProjectAliasRecord> materialized = [.. records];
        var errors = ProjectRegistryValidator.Validate(materialized);
        if (errors.Count > 0)
        {
            throw new ProjectRegistryValidationException(string.Join(" ", errors));
        }

        _records = materialized;
        _pathPrefixes = [.. materialized
            .SelectMany(record => record.Paths, (record, path) => (record.Alias, ProjectRegistryValidator.NormalizePath(path)))
            .OrderByDescending(entry => entry.Item2.Length)];
    }

    public IReadOnlyList<ProjectAliasRecord> List() => _records;

    public ProjectAliasRecord? Validate(string? projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return null;
        }

        return _records.FirstOrDefault(record => record.Alias == projectId);
    }

    public ProjectAliasRecord? ResolveByPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var candidate = ProjectRegistryValidator.NormalizePath(path);
        foreach (var (alias, prefix) in _pathPrefixes)
        {
            if (MatchesPrefix(candidate, prefix))
            {
                return _records.Single(record => record.Alias == alias);
            }
        }

        return null;
    }

    private static bool MatchesPrefix(string candidate, string prefix)
    {
        if (!candidate.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        return candidate.Length == prefix.Length
            || prefix[^1] == Path.DirectorySeparatorChar
            || candidate[prefix.Length] == Path.DirectorySeparatorChar;
    }
}
