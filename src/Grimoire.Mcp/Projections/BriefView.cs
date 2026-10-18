namespace Grimoire.Mcp.Projections;

internal sealed record BriefView
{
    public required BriefPersonaSection Persona { get; init; }

    public required BriefItemsSection<BriefFactItem> Facts { get; init; }

    public required BriefItemsSection<BriefTombstoneItem> Tombstones { get; init; }

    public required BriefItemsSection<BriefJournalItem> Journal { get; init; }

    public required IReadOnlyList<string> Omitted { get; init; }
}
