namespace Grimoire.Core;

public sealed record MemoryStats
{
    public required long Active { get; init; }

    public required long Superseded { get; init; }

    public required long Tombstoned { get; init; }

    public required IReadOnlyDictionary<MemoryScope, long> ActiveByScope { get; init; }

    public required long PendingCompaction { get; init; }

    public required long IdempotencyConflicts { get; init; }

    public required IReadOnlyDictionary<string, TomeStats> ByTome { get; init; }

    public required GcState Gc { get; init; }
}
