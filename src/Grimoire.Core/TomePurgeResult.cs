namespace Grimoire.Core;

public sealed record TomePurgeResult
{
    public required DeleteByTomeResult Records { get; init; }

    public required bool ManifestPurged { get; init; }
}
