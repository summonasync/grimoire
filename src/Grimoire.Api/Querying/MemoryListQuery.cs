namespace Grimoire.Api.Querying;

public sealed record MemoryListQuery(
    string? Query,
    string? Scope,
    string? Kind,
    string? Status,
    string? Tag,
    string? ProjectId,
    string? SessionId,
    int? Limit);
