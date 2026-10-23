namespace Grimoire.Mcp.Projections;

internal sealed record ProjectsView
{
    public required IReadOnlyList<ProjectEntryView> Projects { get; init; }

    public required int Count { get; init; }
}
