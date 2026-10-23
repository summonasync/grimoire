namespace Grimoire.Mcp.Projections;

internal sealed record ProjectChangeView
{
    public required string Action { get; init; }

    public required ProjectEntryView Project { get; init; }
}
