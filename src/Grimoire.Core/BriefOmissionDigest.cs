using System.Text;

namespace Grimoire.Core;

internal static class BriefOmissionDigest
{
    // Each path re-sorts on the keys its own contract declares instead of trusting the order the
    // collect paths happened to enumerate: the upstream remainders lean on LINQ group-creation order,
    // which is an implementation behavior. Every digest test seeds an order that contradicts these
    // chains, so a regression to inherited enumeration goes red here.
    internal static IReadOnlyDictionary<BriefSection, BriefSectionOmissions>? Build(
        IReadOnlyList<BriefOmissionLine> facts,
        IReadOnlyList<BriefOmissionLine> tombstones,
        IReadOnlyList<BriefOmissionLine> journal,
        IReadOnlyList<BriefOmissionLine> daily,
        MemoryEngineOptions options)
    {
        var sections = new (BriefSection Section, IReadOnlyList<BriefOmissionLine> Lines)[]
        {
            (BriefSection.Facts, SortFacts(facts)),
            (BriefSection.Tombstones, SortTombstones(tombstones)),
            (BriefSection.Journal, SortJournal(journal)),
            (BriefSection.Daily, SortDaily(daily)),
        };

        Dictionary<BriefSection, BriefSectionOmissions>? digest = null;
        foreach (var (section, lines) in sections)
        {
            if (lines.Count == 0)
            {
                continue;
            }

            digest ??= [];
            digest[section] = new BriefSectionOmissions
            {
                Count = lines.Count,
                Dropped = [.. lines.Select(line => Shape(line, options))],
            };
        }

        return digest;
    }

    private static IReadOnlyList<BriefOmissionLine> SortFacts(IReadOnlyList<BriefOmissionLine> lines) =>
        [.. lines
            .OrderBy(line => line.GroupRank)
            .ThenByDescending(line => line.Score)
            .ThenByDescending(line => line.Stamp)
            .ThenBy(line => line.Id)];

    private static IReadOnlyList<BriefOmissionLine> SortTombstones(IReadOnlyList<BriefOmissionLine> lines) =>
        [.. lines
            .OrderByDescending(line => line.Stamp)
            .ThenBy(line => line.Id)];

    private static IReadOnlyList<BriefOmissionLine> SortJournal(IReadOnlyList<BriefOmissionLine> lines) =>
        [.. lines
            .OrderBy(line => line.GroupRank)
            .ThenByDescending(line => line.Stamp)
            .ThenBy(line => line.Id)];

    private static IReadOnlyList<BriefOmissionLine> SortDaily(IReadOnlyList<BriefOmissionLine> lines) =>
        [.. lines
            .OrderBy(line => line.GroupRank)
            .ThenBy(line => line.Actor, StringComparer.OrdinalIgnoreCase)
            .ThenBy(line => line.Stamp)
            .ThenBy(line => line.Id)];

    private static BriefOmittedRecord Shape(BriefOmissionLine line, MemoryEngineOptions options)
    {
        var text = SingleLine(line.Text);
        if (text.Length <= Math.Max(0, options.BriefNeverTruncateChars))
        {
            return new BriefOmittedRecord
            {
                MemoryId = line.Id,
                Excerpt = text,
                Reason = line.Reason,
            };
        }

        var width = Math.Clamp(options.BriefDigestExcerptChars, 1, text.Length);
        return new BriefOmittedRecord
        {
            MemoryId = line.Id,
            Excerpt = text[..width],
            Reason = line.Reason,
            ExcerptCut = true,
        };
    }

    private static string SingleLine(string text)
    {
        if (!text.Any(char.IsWhiteSpace))
        {
            return text;
        }

        var folded = new StringBuilder(text.Length);
        var pending = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                pending = folded.Length > 0;
                continue;
            }

            if (pending)
            {
                folded.Append(' ');
                pending = false;
            }

            folded.Append(character);
        }

        return folded.ToString();
    }
}
