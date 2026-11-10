namespace Grimoire.Core;

public sealed record TomeManifest
{
    public required string Key { get; init; }

    public required string Description { get; init; }

    public required string CreatedBy { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
