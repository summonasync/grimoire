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
    IReadOnlyDictionary<string, TomeStats> ByTome);
