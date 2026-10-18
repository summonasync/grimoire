namespace Grimoire.Core;

public sealed record MemoryPoint
{
    public required MemoryRecord Record { get; init; }

    public required ReadOnlyMemory<float> Vector { get; init; }
}
