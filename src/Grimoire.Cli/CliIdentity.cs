using Grimoire.Core;

namespace Grimoire.Cli;

internal sealed record CliIdentity
{
    public required MemoryActor Actor { get; init; }

    public required string Tome { get; init; }

    public required TomeSource Source { get; init; }
}
