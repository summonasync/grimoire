namespace Grimoire.Core;

public sealed record AutoMemoryFileOutcome
{
    public required string FileName { get; init; }

    public required AutoMemoryOutcomeKind Kind { get; init; }

    public Guid? RecordId { get; init; }

    public Guid? WinnerId { get; init; }

    public string? Reason { get; init; }
}
