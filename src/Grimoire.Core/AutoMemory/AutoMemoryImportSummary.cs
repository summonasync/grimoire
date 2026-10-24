namespace Grimoire.Core;

public sealed record AutoMemoryImportSummary
{
    public required int Files { get; init; }

    public required int Unchanged { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    public required IReadOnlyList<AutoMemoryFileOutcome> Outcomes { get; init; }

    public int Imported => Outcomes.Count(outcome => outcome.Kind == AutoMemoryOutcomeKind.Imported);

    public int SkippedDuplicates => Outcomes.Count(outcome => outcome.Kind == AutoMemoryOutcomeKind.SkippedDuplicate);

    public int Failed => Outcomes.Count(outcome => outcome.Kind == AutoMemoryOutcomeKind.Failed);
}
