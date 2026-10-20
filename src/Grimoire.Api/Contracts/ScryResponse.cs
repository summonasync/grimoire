namespace Grimoire.Api.Contracts;

public sealed record ScryResponse(
    IReadOnlyList<string> Tomes,
    ScryAttribution Attribution,
    int Count,
    IReadOnlyList<ScryItem> Items,
    long ElapsedMs);
