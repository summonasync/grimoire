using Grimoire.Core;

namespace Grimoire.Api.Querying;

public static class GraphClassesParser
{
    public static IReadOnlyList<RecordEdgeClass> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [RecordEdgeClass.Supersede, RecordEdgeClass.Link, RecordEdgeClass.Similarity];
        }

        var classes = new List<RecordEdgeClass>();
        foreach (var token in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            classes.Add(token.ToLowerInvariant() switch
            {
                "a" => RecordEdgeClass.Supersede,
                "b" => RecordEdgeClass.Link,
                "c" => RecordEdgeClass.Similarity,
                "d" => RecordEdgeClass.CoMembership,
                _ => throw new MemoryQueryException(
                    "invalid_class",
                    $"Unknown graph class '{token}'. Use a (supersede), b (link), c (similarity) or d (co-membership)."),
            });
        }

        return classes;
    }
}
