namespace Grimoire.Core;

public sealed record ConsolidationOptions
{
    public const string Tag = "consolidated";

    public TimeSpan SweepInterval { get; init; } = TimeSpan.FromHours(6);

    public int MinConsolidationSize { get; init; } = 4;

    public TimeSpan StalenessThreshold => SweepInterval * 2;
}
