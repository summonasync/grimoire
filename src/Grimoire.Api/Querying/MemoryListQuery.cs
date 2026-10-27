namespace Grimoire.Api.Querying;

public sealed record MemoryListQuery(
    string? Query,
    string? Scope,
    string? Kind,
    string? Status,
    string? Tag,
    string? ProjectId,
    string? SessionId,
    string? Tome,
    int? Limit,
    string? Since,
    string? Until,
    string? Actor,
    string? Fields);
