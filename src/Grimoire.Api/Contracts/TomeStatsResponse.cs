namespace Grimoire.Api.Contracts;

public sealed record TomeStatsResponse(long Active, long Tombstoned, DateTimeOffset LastWriteAt);
