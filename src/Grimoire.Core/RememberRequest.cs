namespace Grimoire.Core;

public sealed record RememberRequest
{
    public required string Content { get; init; }

    public required MemoryScope Scope { get; init; }

    public MemoryKind Kind { get; init; } = MemoryKind.Journal;

    public required MemoryActor Actor { get; init; }

    public string? Tome { get; init; }

    public string? ProjectId { get; init; }

    public string? SessionId { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public double Importance { get; init; } = 0.5;

    public IReadOnlyList<Guid> Sources { get; init; } = [];

    public Guid? Supersedes { get; init; }
}
