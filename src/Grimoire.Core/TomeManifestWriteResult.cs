namespace Grimoire.Core;

public sealed record TomeManifestWriteResult
{
    public required TomeManifestOutcome Outcome { get; init; }

    public TomeManifest? Manifest { get; init; }
}
