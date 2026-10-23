namespace Grimoire.Core;

public sealed class LiveProjectRegistry(IProjectRegistryStore store) : IProjectRegistry
{
    public IReadOnlyList<ProjectAliasRecord> List() => Current().List();

    public ProjectAliasRecord? Validate(string? projectId) => Current().Validate(projectId);

    public ProjectAliasRecord? ResolveByPath(string? path) => Current().ResolveByPath(path);

    private ProjectRegistry Current() => new(store.Read());
}
