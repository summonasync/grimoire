namespace Grimoire.Api;

public sealed record ApiHostOptions
{
    public int Port { get; init; } = 4770;

    public int GcIntervalMinutes { get; init; } = 30;

    public required string StaticRootPath { get; init; }

    // Test-only: keeps configuration-sourced Kestrel endpoints loadable so tests can prove
    // the post-bind loopback guard. Production paths keep the configuration wipe.
    public bool DisableConfigSourceClear { get; init; }
}
