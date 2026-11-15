namespace Grimoire.Mcp.Projections;

internal sealed record BriefDigestSection
{
    public required int Count { get; init; }

    public required IReadOnlyList<BriefOmittedItem> Dropped { get; init; }
}
