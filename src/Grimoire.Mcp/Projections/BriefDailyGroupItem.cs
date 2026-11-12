using Grimoire.Core;

namespace Grimoire.Mcp.Projections;

internal sealed record BriefDailyGroupItem
{
    public required DateOnly Day { get; init; }

    public required BriefDailyBucket Bucket { get; init; }

    public required string Actor { get; init; }

    public required IReadOnlyList<BriefDailyEntryItem> Entries { get; init; }
}
