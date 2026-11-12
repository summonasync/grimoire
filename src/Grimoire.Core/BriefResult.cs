using System.Text.Json.Serialization;

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

    public required BriefSectionState TomesState { get; init; }

    public required IReadOnlyList<BriefTome> Tomes { get; init; }

    public int TomesMore { get; init; }

    public string? TomesNote { get; init; }

    public BriefSectionState DailyState { get; init; } = BriefSectionState.Empty;

    public IReadOnlyList<BriefDailyGroup> DailyGroups { get; init; } = [];

    public int DailyMore { get; init; }

    public required IReadOnlyList<BriefSection> Omitted { get; init; }

    public BriefCompactionDue? CompactionDue { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Warnings { get; init; }
}
