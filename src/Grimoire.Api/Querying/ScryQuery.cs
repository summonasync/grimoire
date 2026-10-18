namespace Grimoire.Api.Querying;

public sealed record ScryQuery(
    string? Q,
    string? Tomes,
    bool? IncludeJournal,
    string? Attribution,
    int? Limit);
