namespace Grimoire.Core;

public sealed record BriefResult
{
    public required BriefSectionState PersonaState { get; init; }

    public required PersonaDocument? Persona { get; init; }

    public required BriefSectionState FactsState { get; init; }

    public required IReadOnlyList<BriefFact> Facts { get; init; }

    public required BriefSectionState TombstonesState { get; init; }

    public required IReadOnlyList<BriefTombstone> Tombstones { get; init; }

    public required BriefSectionState JournalState { get; init; }

    public required IReadOnlyList<BriefJournalEntry> Journal { get; init; }

    public required IReadOnlyList<BriefSection> Omitted { get; init; }
}
