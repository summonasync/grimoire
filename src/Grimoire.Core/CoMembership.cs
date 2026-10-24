namespace Grimoire.Core;

public static class CoMembership
{
    public static IReadOnlyList<RecordEdge> Edges(IReadOnlyList<MemoryRecord> records)
    {
        var active = records.Where(record => record.Status == MemoryStatus.Active).ToArray();
        var statusById = active.ToDictionary(record => record.Id, record => record.Status);
        var pairs = new HashSet<(Guid From, Guid To)>();

        CollectPairs(active, pairs, TagsOf);
        CollectPairs(active, pairs, ProjectsOf);
        CollectPairs(active, pairs, ActorsOf);

        return [.. pairs
            .OrderBy(pair => pair.From)
            .ThenBy(pair => pair.To)
            .Select(pair => new RecordEdge
            {
                FromId = pair.From,
                ToId = pair.To,
                Class = RecordEdgeClass.CoMembership,
                TargetStatus = statusById[pair.To],
            })];
    }

    private static IEnumerable<string> TagsOf(MemoryRecord record) => record.Tags;

    private static IEnumerable<string> ProjectsOf(MemoryRecord record) =>
        record.ProjectId is { } project ? [project] : [];

    private static IEnumerable<string> ActorsOf(MemoryRecord record) => [record.Actor.ToString()];

    private static void CollectPairs(
        IReadOnlyList<MemoryRecord> records,
        HashSet<(Guid From, Guid To)> pairs,
        Func<MemoryRecord, IEnumerable<string>> keysOf)
    {
        var groups = new Dictionary<string, List<Guid>>(StringComparer.Ordinal);

        foreach (var record in records)
        {
            foreach (var key in keysOf(record))
            {
                if (!groups.TryGetValue(key, out var members))
                {
                    members = [];
                    groups.Add(key, members);
                }

                members.Add(record.Id);
            }
        }

        foreach (var members in groups.Values)
        {
            for (var i = 0; i < members.Count; i++)
            {
                for (var j = i + 1; j < members.Count; j++)
                {
                    if (members[i] != members[j])
                    {
                        pairs.Add(Pair(members[i], members[j]));
                    }
                }
            }
        }
    }

    private static (Guid From, Guid To) Pair(Guid left, Guid right) =>
        left.CompareTo(right) <= 0 ? (left, right) : (right, left);
}
