namespace Grimoire.Core;

public sealed record ProjectSyncReport
{
    public required IReadOnlyList<ProjectSyncChange> Added { get; init; }

    public required IReadOnlyList<ProjectSyncChange> Updated { get; init; }

    public required IReadOnlyList<ProjectSyncChange> Gone { get; init; }

    public required IReadOnlyList<ProjectSyncChange> Skipped { get; init; }
}
