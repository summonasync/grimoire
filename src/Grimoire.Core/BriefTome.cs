namespace Grimoire.Core;

public sealed record BriefTome
{
    public required string Key { get; init; }

    public required string Description { get; init; }

    public required string CreatedBy { get; init; }
}
