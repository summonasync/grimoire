using System.Text.Json.Serialization;
using Grimoire.Core;

namespace Grimoire.Api.Contracts;

public sealed record StatsResponse(
    string Tome,
    long Active,
    long Superseded,
    long Tombstoned,
    IReadOnlyDictionary<MemoryScope, long> ActiveByScope,
    long PendingCompaction,
    long IdempotencyConflicts,
    IReadOnlyDictionary<string, TomeStatsResponse> ByTome,
    IReadOnlyDictionary<string, int> ByProject,
    GcState Gc)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, long>? PendingCompactionByTome { get; init; }
}
