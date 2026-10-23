using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

internal sealed class FakeProjectRegistryStore(IReadOnlyList<ProjectAliasRecord>? initial = null) : IProjectRegistryStore
{
    private readonly List<ProjectAliasRecord> _records = initial is null ? [] : [.. initial];

    public int Mutations { get; private set; }

    public IReadOnlyList<ProjectAliasRecord> Records => _records;

    public IReadOnlyList<ProjectAliasRecord> Read() => _records;

    public string Mutate(Func<List<ProjectAliasRecord>, string> mutate)
    {
        var working = new List<ProjectAliasRecord>(_records);
        var report = mutate(working);
        var errors = ProjectRegistryValidator.Validate(working);
        if (errors.Count > 0)
        {
            throw new ProjectRegistryMutationException($"Project registry is invalid: {string.Join(" ", errors)}");
        }

        _records.Clear();
        _records.AddRange(working);
        Mutations++;
        return report;
    }
}
