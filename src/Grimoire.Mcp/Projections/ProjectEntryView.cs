namespace Grimoire.Mcp.Projections;

internal sealed record ProjectEntryView
{
    public required string Alias { get; init; }

    public IReadOnlyList<string> Paths { get; init; } = [];

    public string? BoardProjectId { get; init; }

    public string? DisplayName { get; init; }
}
