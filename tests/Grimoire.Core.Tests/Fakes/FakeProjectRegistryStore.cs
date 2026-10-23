namespace Grimoire.Core.Tests.Fakes;

public sealed class FakeProjectRegistryStore(IReadOnlyList<ProjectAliasRecord>? initial = null) : IProjectRegistryStore
{
    public Action<List<ProjectAliasRecord>>? Interceptor { get; set; }

    public List<ProjectAliasRecord> Records { get; } = initial is null ? [] : [.. initial];

    public IReadOnlyList<ProjectAliasRecord> Read() => [.. Records];

    public string Mutate(Func<List<ProjectAliasRecord>, string> mutate)
    {
        Interceptor?.Invoke(Records);
        var working = new List<ProjectAliasRecord>(Records);
        var report = mutate(working);
        Records.Clear();
        Records.AddRange(working);
        return report;
    }
}
