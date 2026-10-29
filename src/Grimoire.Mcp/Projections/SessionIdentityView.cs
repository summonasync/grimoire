namespace Grimoire.Mcp.Projections;

internal sealed record SessionIdentityView
{
    public required string Alias { get; init; }

    public required string? Vendor { get; init; }

    public required string AppliesTo { get; init; }
}
