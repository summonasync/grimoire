namespace Grimoire.Core;

public static class MemoryStatuses
{
    public static IReadOnlyList<MemoryStatus> All { get; } = [.. Enum.GetValues<MemoryStatus>()];

    // Which statuses a filter means. An absent clause is Active, not "everything" — the store
    // has always read it that way, and this is the single place that says so.
    public static IReadOnlyList<MemoryStatus> Effective(MemoryFilter filter) =>
        filter.Statuses.Count > 0
            ? filter.Statuses
            : [filter.Status ?? MemoryStatus.Active];
}
