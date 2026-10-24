namespace Grimoire.Core;

public static class RecordGraphProjection
{
    public static RecordGraph Project(IReadOnlyList<MemoryRecord> records, RecordGraphOptions? options = null)
    {
        var edges = CollectEdges(records, options);
        return AsGraph(edges);
    }

    public static RecordGraph ProjectPoints(IReadOnlyList<MemoryPoint> points, RecordGraphOptions? options = null)
    {
        var edges = CollectEdges(points.Select(point => point.Record).ToArray(), options);

        if (options?.RelatedSimilarityThreshold is { } threshold)
        {
            edges.UnionWith(SimilarityEdges.Edges(points, threshold));
        }

        return AsGraph(edges);
    }

    private static HashSet<RecordEdge> CollectEdges(IReadOnlyList<MemoryRecord> records, RecordGraphOptions? options)
    {
        var byId = records.ToDictionary(record => record.Id);
        var edges = new HashSet<RecordEdge>();

        foreach (var record in records)
        {
            if (record.SupersededBy is { } successor
                && successor != record.Id
                && byId.TryGetValue(successor, out var successorRecord))
            {
                edges.Add(new RecordEdge
                {
                    FromId = record.Id,
                    ToId = successor,
                    Class = RecordEdgeClass.Supersede,
                    TargetStatus = successorRecord.Status,
                });
            }

            foreach (var target in RecordLinks(record))
            {
                if (target == record.Id || !byId.TryGetValue(target, out var targetRecord))
                {
                    continue;
                }

                edges.Add(new RecordEdge
                {
                    FromId = record.Id,
                    ToId = target,
                    Class = RecordEdgeClass.Link,
                    TargetStatus = targetRecord.Status,
                });
            }
        }

        if (options?.IncludeCoMembership == true)
        {
            edges.UnionWith(CoMembership.Edges(records));
        }

        return edges;
    }

    private static RecordGraph AsGraph(HashSet<RecordEdge> edges) =>
        new()
        {
            Edges = [.. edges.OrderBy(edge => edge.Class).ThenBy(edge => edge.FromId).ThenBy(edge => edge.ToId)],
        };

    private static IEnumerable<Guid> RecordLinks(MemoryRecord record)
    {
        foreach (var id in RecordLinkParser.Parse(record.Content))
        {
            yield return id;
        }

        foreach (var id in RecordLinkParser.ParseReason(record.TombstoneReason))
        {
            yield return id;
        }
    }
}
