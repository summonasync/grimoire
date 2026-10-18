namespace Grimoire.Core;

public sealed record ActorClaim
{
    public string? Alias { get; init; }

    public string? Tome { get; init; }
}
