namespace Grimoire.Core;

public sealed record RecordGraph
{
    public required IReadOnlyList<RecordEdge> Edges { get; init; }
}
