namespace Grimoire.Core;

// What an actor candidate means where records are selected. The candidate travels as one string
// ("alias" or "alias+vendor"), while a stored actor is a pair, so the meaning is a split — and the
// split needs one home for the same reason the status rule does: a translator and four in-process
// doubles holding private opinions is how a dimension ends up enforced in prose and nowhere else.
//
// Exact comparison is deliberate. The matcher in MemoryFilterExtensions.MatchesActor compares
// case-insensitively and stays that way on purpose; this rule does not, because the write path
// normalizes nothing, so a mixed-case candidate narrows harder here than there. The difference is
// pinned by tests in every harness rather than resolved.
public static class MemoryActorSemantics
{
    public static IReadOnlyList<(string Alias, string? Vendor)> Segments(string? candidate)
    {
        if (MemoryActor.AliasPart(candidate) is not { } alias)
        {
            return [];
        }

        var separator = candidate!.IndexOf('+', StringComparison.Ordinal);
        var vendor = separator < 0 ? null : candidate[(separator + 1)..];

        return string.IsNullOrEmpty(vendor) ? [(alias, null)] : [(alias, vendor)];
    }

    public static bool MatchesExactly(MemoryActor actor, string? candidate)
    {
        var segments = Segments(candidate);

        if (segments.Count == 0)
        {
            return true;
        }

        var (alias, vendor) = segments[0];

        return actor.Alias == alias && (vendor is null || actor.Vendor == vendor);
    }
}
