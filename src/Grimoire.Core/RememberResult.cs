namespace Grimoire.Core;

public sealed record RememberResult
{
    public required MemoryRecord Record { get; init; }

    public Guid? SupersededId { get; init; }

    public bool SupersededExisting => SupersededId is not null;
}
