namespace Grimoire.Infrastructure.Configuration;

public sealed record RecencyHalfLifeOptions
{
    public double Session { get; init; } = 0.5;

    public double Project { get; init; } = 30;

    public double Global { get; init; } = 90;

    public double Personal { get; init; } = 180;
}
