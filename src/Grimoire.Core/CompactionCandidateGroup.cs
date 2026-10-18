namespace Grimoire.Core;

public sealed record CompactionCandidateGroup
{
    public required IReadOnlyList<Guid> MemoryIds { get; init; }

    public required double Similarity { get; init; }

    public required DateTimeOffset StalestAccess { get; init; }
}
