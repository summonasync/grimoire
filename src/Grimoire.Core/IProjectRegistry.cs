namespace Grimoire.Core;

public interface IProjectRegistry
{
    IReadOnlyList<ProjectAliasRecord> List();

    ProjectAliasRecord? Validate(string? projectId);

    ProjectAliasRecord? ResolveByPath(string? path);
}
