namespace Grimoire.Api.Contracts;

public sealed record ProjectedScryResponse(
    IReadOnlyList<string> Tomes,
    ScryAttribution Attribution,
    int Count,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Items,
    long ElapsedMs);
