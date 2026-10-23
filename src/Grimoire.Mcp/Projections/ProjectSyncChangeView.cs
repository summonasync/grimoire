namespace Grimoire.Mcp.Projections;

internal sealed record ProjectSyncChangeView
{
    public required string Subject { get; init; }

    public required string Detail { get; init; }
}
