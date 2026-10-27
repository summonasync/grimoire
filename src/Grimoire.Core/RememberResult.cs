namespace Grimoire.Core;

public sealed record RememberResult
{
    public required MemoryRecord Record { get; init; }

    public Guid? SupersededId { get; init; }

    public bool SupersededExisting => SupersededId is not null;

    public bool IdempotentReplay { get; init; }

    public bool MaintenancePending { get; init; }

    public IReadOnlyList<NearMatch> NearMatches { get; init; } = [];
}
