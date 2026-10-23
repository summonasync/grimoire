namespace Grimoire.Core;

public sealed class ProjectSyncService(IBoardProjectSource source, IProjectRegistryStore store)
{
    public async Task<ProjectSyncReport> SyncAsync(CancellationToken cancellationToken)
    {
        var board = await source.FetchAsync(cancellationToken);
        var snapshot = store.Read();
        var plan = ProjectSyncPlanner.Plan(snapshot, board);
        if (!plan.NeedsWrite)
        {
            return plan.Report;
        }

        store.Mutate(records =>
        {
            EnsureUnchanged(snapshot, records);
            Apply(records, plan);
            return Summary(plan.Report);
        });

        return plan.Report;
    }

    private static void EnsureUnchanged(IReadOnlyList<ProjectAliasRecord> snapshot, List<ProjectAliasRecord> current)
    {
        if (snapshot.Count == current.Count
            && snapshot.Zip(current, SameRecord).All(same => same))
        {
            return;
        }

        throw new ProjectRegistryStaleException(
            "The project registry changed while the sync was reading the board; nothing was written. " +
            "Re-run the sync so it applies against the current registry.");
    }

    private static void Apply(List<ProjectAliasRecord> records, ProjectSyncPlan plan)
    {
        records.AddRange(plan.Added);
        foreach (var updated in plan.Updated)
        {
            records[records.FindIndex(record => record.Alias == updated.Alias)] = updated;
        }

        foreach (var alias in plan.GoneAliases)
        {
            var index = records.FindIndex(record => record.Alias == alias);
            records[index] = records[index] with { BoardGone = true };
        }
    }

    private static bool SameRecord(ProjectAliasRecord left, ProjectAliasRecord right) =>
        left.Alias == right.Alias
        && left.BoardGone == right.BoardGone
        && left.BoardProjectId == right.BoardProjectId
        && left.DisplayName == right.DisplayName
        && left.Description == right.Description
        && left.Paths.SequenceEqual(right.Paths, StringComparer.Ordinal);

    private static string Summary(ProjectSyncReport report) =>
        $"Synced the project registry: {report.Added.Count} added, {report.Updated.Count} updated, " +
        $"{report.Gone.Count} board gone, {report.Skipped.Count} skipped.";
}
