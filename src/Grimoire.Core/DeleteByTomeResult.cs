namespace Grimoire.Core;

public sealed record DeleteByTomeResult
{
    public required int Active { get; init; }

    public required int Superseded { get; init; }

    public required int Tombstoned { get; init; }
}
