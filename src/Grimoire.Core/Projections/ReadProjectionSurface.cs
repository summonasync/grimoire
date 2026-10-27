namespace Grimoire.Core;

public sealed record ReadProjectionSurface
{
    public required IReadOnlyList<string> AllowedKeys { get; init; }

    public required IReadOnlyList<string> AlwaysOnKeys { get; init; }

    public required IReadOnlyList<string> CompactKeys { get; init; }
}
