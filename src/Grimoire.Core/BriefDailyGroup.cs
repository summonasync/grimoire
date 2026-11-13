namespace Grimoire.Core;

public sealed record BriefDailyGroup
{
    public required DateOnly Day { get; init; }

    public required BriefDailyBucket Bucket { get; init; }

    public required string Actor { get; init; }

    public required IReadOnlyList<BriefDailyEntry> Entries { get; init; }
}
