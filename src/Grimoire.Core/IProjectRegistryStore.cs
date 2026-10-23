namespace Grimoire.Core;

public interface IProjectRegistryStore
{
    IReadOnlyList<ProjectAliasRecord> Read();

    string Mutate(Func<List<ProjectAliasRecord>, string> mutate);
}
