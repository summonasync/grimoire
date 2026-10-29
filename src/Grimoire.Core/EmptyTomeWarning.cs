namespace Grimoire.Core;

public static class EmptyTomeWarning
{
    public static async Task<IReadOnlyList<string>?> BuildAsync(
        IMemoryStore store,
        bool isEmpty,
        string? requestedTome,
        string resolvedTome,
        CancellationToken cancellationToken)
    {
        if (!isEmpty || requestedTome is null)
        {
            return null;
        }

        var known = await KnownTomeKeys.ListAsync(store, cancellationToken);
        return known.Contains(resolvedTome)
            ? null
            : [Message(resolvedTome, known)];
    }

    private static string Message(string tome, IReadOnlyList<string> known)
    {
        var knownList = known.Count == 0 ? "(none yet)" : string.Join(", ", known);
        return $"tome '{tome}' is empty or does not exist. Known tome keys: {knownList}";
    }
}
