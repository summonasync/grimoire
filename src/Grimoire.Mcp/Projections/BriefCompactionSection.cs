namespace Grimoire.Mcp.Projections;

internal sealed record BriefCompactionSection
{
    public required long PendingEntries { get; init; }
}
