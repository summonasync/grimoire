namespace Grimoire.Mcp.Projections;

internal sealed record DigestView
{
    public required string Tome { get; init; }

    public required string Topic { get; init; }

    public required BriefItemsSection<DigestHitItem> LongMemo { get; init; }

    public required BriefItemsSection<DigestHitItem> Recent { get; init; }

    public required BriefItemsSection<DigestHitItem> Gone { get; init; }

    public required IReadOnlyList<string> Omitted { get; init; }
}
