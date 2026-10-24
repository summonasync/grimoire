namespace Grimoire.Core;

public sealed record AutoMemoryImportStateRead
{
    public AutoMemoryImportState? State { get; init; }

    public string? Warning { get; init; }
}
