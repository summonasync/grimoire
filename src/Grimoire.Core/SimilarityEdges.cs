namespace Grimoire.Core;

public static class SimilarityEdges
{
    public static IReadOnlyList<RecordEdge> Edges(IReadOnlyList<MemoryPoint> points, double threshold)
    {
        var active = points
            .Where(point => point.Record.Status == MemoryStatus.Active)
            .DistinctBy(point => point.Record.Id)
            .ToArray();
        var statusById = active.ToDictionary(point => point.Record.Id, point => point.Record.Status);
        var pairs = new HashSet<(Guid From, Guid To)>();

        for (var i = 0; i < active.Length; i++)
        {
            for (var j = i + 1; j < active.Length; j++)
            {
                if (active[i].Record.Tome != active[j].Record.Tome)
                {
                    continue;
                }

                if (VectorMath.CosineSimilarity(active[i].Vector.Span, active[j].Vector.Span) < threshold)
                {
                    continue;
                }

                var pair = Pair(active[i].Record.Id, active[j].Record.Id);
                if (pair.From != pair.To)
                {
                    pairs.Add(pair);
                }
            }
        }

        return [.. pairs
            .OrderBy(pair => pair.From)
            .ThenBy(pair => pair.To)
            .Select(pair => new RecordEdge
            {
                FromId = pair.From,
                ToId = pair.To,
                Class = RecordEdgeClass.Similarity,
                TargetStatus = statusById[pair.To],
            })];
    }

    private static (Guid From, Guid To) Pair(Guid left, Guid right) =>
        left.CompareTo(right) <= 0 ? (left, right) : (right, left);
}
