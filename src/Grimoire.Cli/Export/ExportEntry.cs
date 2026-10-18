using Grimoire.Core;

namespace Grimoire.Cli.Export;

internal sealed record ExportEntry
{
    public required MemoryRecord Record { get; init; }

    public required float[] Vector { get; init; }
}
