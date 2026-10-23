using Grimoire.Core;

namespace Grimoire.Mcp.Projections;

internal sealed record ProjectSyncView
{
    public required IReadOnlyList<ProjectSyncChangeView> Added { get; init; }

    public required IReadOnlyList<ProjectSyncChangeView> Updated { get; init; }

    public required IReadOnlyList<ProjectSyncChangeView> Gone { get; init; }

    public required IReadOnlyList<ProjectSyncChangeView> Skipped { get; init; }

    public static ProjectSyncView From(ProjectSyncReport report) => new()
    {
        Added = [.. report.Added.Select(ToView)],
        Updated = [.. report.Updated.Select(ToView)],
        Gone = [.. report.Gone.Select(ToView)],
        Skipped = [.. report.Skipped.Select(ToView)],
    };

    private static ProjectSyncChangeView ToView(ProjectSyncChange change) => new()
    {
        Subject = change.Subject,
        Detail = change.Detail,
    };
}
