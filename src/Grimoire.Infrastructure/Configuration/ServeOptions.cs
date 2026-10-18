namespace Grimoire.Infrastructure.Configuration;

public sealed record ServeOptions
{
    public int Port { get; init; } = 4770;

    public double GcIntervalMinutes { get; init; } = 30;
}
