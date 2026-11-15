namespace Grimoire.Core;

public static class WarningMerge
{
    // Two sources land in one channel — core and the door's own tome check — so neither may
    // overwrite the other. Silence stays null: an empty list would serialize a key onto a
    // payload that has nothing to report. Order is core first, then tome.
    public static IReadOnlyList<string>? Merge(IReadOnlyList<string>? coreWarnings, IReadOnlyList<string>? tomeWarnings)
    {
        if (coreWarnings is null)
        {
            return tomeWarnings;
        }

        return tomeWarnings is null
            ? coreWarnings
            : [.. coreWarnings.Concat(tomeWarnings).Distinct(StringComparer.Ordinal)];
    }
}
