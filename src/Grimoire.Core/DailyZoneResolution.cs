namespace Grimoire.Core;

public enum DailyZoneSource
{
    Configured,

    MachineLocal,

    UtcFallback,
}

public sealed record DailyZoneResolution
{
    public required TimeZoneInfo Zone { get; init; }

    public required DailyZoneSource Source { get; init; }
}
