using Grimoire.Core;

namespace Grimoire.Mcp.Projections;

internal sealed record StatsView
{
    public required string Tome { get; init; }

    public required long Active { get; init; }

    public required long Superseded { get; init; }

    public required long Tombstoned { get; init; }

    public required IReadOnlyDictionary<MemoryScope, long> ActiveByScope { get; init; }

    public required long PendingCompaction { get; init; }

    public required long IdempotencyConflicts { get; init; }

    public required IReadOnlyDictionary<string, TomeStats> ByTome { get; init; }

    public IReadOnlyDictionary<string, long>? PendingCompactionByTome { get; init; }

    public required GcState Gc { get; init; }

    public required ConsolidationState Consolidation { get; init; }

    public IReadOnlyList<string>? Warnings { get; init; }
}
