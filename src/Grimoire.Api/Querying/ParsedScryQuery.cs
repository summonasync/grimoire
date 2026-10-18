using Grimoire.Api.Contracts;

namespace Grimoire.Api.Querying;

public sealed record ParsedScryQuery(
    string Query,
    IReadOnlyList<string> Tomes,
    bool IncludeJournal,
    ScryAttribution Attribution,
    int Limit);
