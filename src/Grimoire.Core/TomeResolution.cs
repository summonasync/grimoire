namespace Grimoire.Core;

public enum TomeSource
{
    ExplicitParam,

    EnvTome,

    EnvActor,

    ClaimFile,

    ClientInfo,

    Default,
}

public sealed record TomeResolution
{
    public required string ResolvedTome { get; init; }

    public required TomeSource Source { get; init; }
}
