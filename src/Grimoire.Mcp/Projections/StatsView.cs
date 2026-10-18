using Grimoire.Core;

namespace Grimoire.Mcp.Projections;

internal sealed record StatsView
{
    public required long Active { get; init; }

    public required long Superseded { get; init; }

    public required long Tombstoned { get; init; }

    public required IReadOnlyDictionary<MemoryScope, long> ActiveByScope { get; init; }

    public required long PendingCompaction { get; init; }
}
