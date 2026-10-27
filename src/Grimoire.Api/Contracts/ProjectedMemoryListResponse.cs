namespace Grimoire.Api.Contracts;

public sealed record ProjectedMemoryListResponse(
    string Tome,
    int Count,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Items);
