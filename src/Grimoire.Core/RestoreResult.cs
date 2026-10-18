namespace Grimoire.Core;

public sealed record RestoreResult
{
    public required Guid Id { get; init; }

    public bool Restored { get; init; }

    public MemoryRecord? Record { get; init; }

    public SupersederState? Superseder { get; init; }
}
