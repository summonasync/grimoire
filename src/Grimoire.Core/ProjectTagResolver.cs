namespace Grimoire.Core;

public sealed class ProjectTagResolver(IProjectRegistry registry)
{
    public ProjectTagResolution Resolve(MemoryScope scope, string? projectId, string? callerPath)
    {
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            return registry.Validate(projectId) is { } known
                ? new ProjectTagResolution { ProjectId = known.Alias }
                : Rejected(ProjectTagRejection.Unknown);
        }

        if (scope != MemoryScope.Project)
        {
            return new ProjectTagResolution();
        }

        return registry.ResolveByPath(callerPath) is { } match
            ? new ProjectTagResolution { ProjectId = match.Alias, AutoTagged = true }
            : Rejected(ProjectTagRejection.Missing);
    }

    private ProjectTagResolution Rejected(ProjectTagRejection rejection) => new()
    {
        Rejection = rejection,
        AllowedAliases = [.. registry.List().Select(record => record.Alias).Order(StringComparer.Ordinal)],
    };
}
