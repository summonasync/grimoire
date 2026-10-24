using Grimoire.Core;

namespace Grimoire.Api.Contracts;

public sealed record StatsResponse(
    string Tome,
    TomeSource TomeSource,
    long Active,
    long Superseded,
    long Tombstoned,
    IReadOnlyDictionary<MemoryScope, long> ActiveByScope,
    long PendingCompaction,
    long IdempotencyConflicts,
    IReadOnlyDictionary<string, TomeStatsResponse> ByTome,
    GcState Gc);
