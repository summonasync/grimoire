namespace Grimoire.Core;

public static class KnownTomeKeys
{
    public static async Task<IReadOnlyList<string>> ListAsync(IMemoryStore store, CancellationToken cancellationToken)
    {
        var active = await store.ListAsync(new MemoryFilter { Status = MemoryStatus.Active }, cancellationToken);
        var tombstoned = await store.ListAsync(new MemoryFilter { Status = MemoryStatus.Tombstoned }, cancellationToken);
        return [.. active.Concat(tombstoned).Select(record => record.Tome).Distinct()
            .OrderBy(tome => tome, StringComparer.Ordinal)];
    }
}
