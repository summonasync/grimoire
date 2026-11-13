namespace Grimoire.Mcp.Projections;

internal sealed record ScryView
{
    public required IReadOnlyList<string> Tomes { get; init; }

    public required ScryAttribution Attribution { get; init; }

    public required IReadOnlyList<ScryHit> Hits { get; init; }

    public required int Count { get; init; }

    public IReadOnlyList<string>? Warnings { get; init; }
}
