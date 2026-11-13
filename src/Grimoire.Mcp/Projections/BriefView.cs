namespace Grimoire.Mcp.Projections;

internal sealed record BriefView
{
    public required string Tome { get; init; }

    public required BriefPersonaSection Persona { get; init; }

    public required BriefItemsSection<BriefFactItem> Facts { get; init; }

    public required BriefItemsSection<BriefTombstoneItem> Tombstones { get; init; }

    public required BriefItemsSection<BriefJournalItem> Journal { get; init; }

    public BriefDailySection? Daily { get; init; }

    public BriefTomesSection? Tomes { get; init; }

    public required IReadOnlyList<string> Omitted { get; init; }

    public string? Hint { get; init; }

    public BriefCompactionSection? CompactionDue { get; init; }

    public IReadOnlyList<string>? Warnings { get; init; }
}
