namespace Grimoire.Core;

public static class CompactionPlanner
{
    public static IReadOnlyList<CompactionCandidateGroup> Plan(
        IReadOnlyList<MemoryPoint> journal,
        DateTimeOffset now,
        CompactionOptions options)
    {
        var stale = journal
            .Where(point => now - point.Record.LastAccessedAt >= TimeSpan.FromDays(options.StalenessDays))
            .OrderBy(point => point.Record.LastAccessedAt)
            .ThenBy(point => point.Record.Id)
            .ToArray();

        var assigned = new bool[stale.Length];
        var groups = new List<CompactionCandidateGroup>();

        for (var seed = 0; seed < stale.Length; seed++)
        {
            if (assigned[seed])
            {
                continue;
            }

            var cluster = new List<int> { seed };
            for (var candidate = seed + 1; candidate < stale.Length; candidate++)
            {
                if (assigned[candidate])
                {
                    continue;
                }

                if (VectorMath.CosineSimilarity(stale[seed].Vector.Span, stale[candidate].Vector.Span) >= options.MinSimilarity)
                {
                    cluster.Add(candidate);
                }
            }

            if (cluster.Count < options.MinClusterSize)
            {
                continue;
            }

            groups.Add(new CompactionCandidateGroup
            {
                MemoryIds = [.. cluster.Select(index => stale[index].Record.Id)],
                Similarity = cluster.Average(index => VectorMath.CosineSimilarity(stale[seed].Vector.Span, stale[index].Vector.Span)),
                StalestAccess = stale[seed].Record.LastAccessedAt,
            });

            foreach (var index in cluster)
            {
                assigned[index] = true;
            }
        }

        return groups;
    }
}
