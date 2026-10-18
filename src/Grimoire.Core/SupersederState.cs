namespace Grimoire.Core;

public sealed record SupersederState
{
    public required Guid Id { get; init; }

    public required MemoryStatus Status { get; init; }

    public string? TombstoneReason { get; init; }
}
