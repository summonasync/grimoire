namespace Grimoire.Core;

public sealed record ProjectSyncPlan
{
    public required IReadOnlyList<ProjectAliasRecord> Added { get; init; }

    public required IReadOnlyList<ProjectAliasRecord> Updated { get; init; }

    public required IReadOnlyList<string> GoneAliases { get; init; }

    public required ProjectSyncReport Report { get; init; }

    public bool NeedsWrite => Added.Count > 0 || Updated.Count > 0 || GoneAliases.Count > 0;
}
