namespace Grimoire.Core;

public sealed record GcOptions
{
    public TimeSpan SweepInterval { get; init; } = TimeSpan.FromMinutes(30);

    public TimeSpan StalenessThreshold => SweepInterval * 2;
}
