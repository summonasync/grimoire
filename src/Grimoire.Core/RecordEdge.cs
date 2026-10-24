namespace Grimoire.Core;

public sealed record RecordEdge
{
    public required Guid FromId { get; init; }

    public required Guid ToId { get; init; }

    public required RecordEdgeClass Class { get; init; }

    public required MemoryStatus TargetStatus { get; init; }
}
