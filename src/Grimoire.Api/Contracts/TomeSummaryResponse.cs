namespace Grimoire.Api.Contracts;

public sealed record TomeSummaryResponse(
    string Key,
    long Active,
    long Tombstoned,
    DateTimeOffset LastWriteAt,
    bool Own);
