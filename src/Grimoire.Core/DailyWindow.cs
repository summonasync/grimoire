namespace Grimoire.Core;

public sealed record DailyWindow
{
    public required DateOnly Day { get; init; }

    public required BriefDailyBucket Bucket { get; init; }

    public required DateTimeOffset CreatedAfter { get; init; }

    public required DateTimeOffset CreatedBefore { get; init; }
}
