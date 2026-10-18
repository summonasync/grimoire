namespace Grimoire.Core;

public sealed record BriefFact
{
    public required Guid Id { get; init; }

    public required string Content { get; init; }

    public required MemoryScope Scope { get; init; }

    public required double Score { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
