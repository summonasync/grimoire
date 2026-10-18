namespace Grimoire.Core;

public sealed record TomeStats
{
    public required long Active { get; init; }

    public required long Tombstoned { get; init; }
}
