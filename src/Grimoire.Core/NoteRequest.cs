namespace Grimoire.Core;

public sealed record NoteRequest
{
    public required string Content { get; init; }

    public MemoryScope Scope { get; init; } = MemoryScope.Project;

    public required MemoryActor Actor { get; init; }

    public string? Tome { get; init; }

    public string? ProjectId { get; init; }

    public string? SessionId { get; init; }

    public string? Topic { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}
