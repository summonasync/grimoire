namespace Grimoire.Core;

public sealed record AutoMemoryParseResult
{
    public required IReadOnlyList<AutoMemoryCandidate> Candidates { get; init; }

    public required IReadOnlyList<AutoMemoryParseFailure> Failures { get; init; }
}
