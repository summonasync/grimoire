namespace Grimoire.Mcp.Projections;

internal sealed record BriefTomeItem
{
    public required string Key { get; init; }

    public required string Description { get; init; }

    public required string CreatedBy { get; init; }
}
