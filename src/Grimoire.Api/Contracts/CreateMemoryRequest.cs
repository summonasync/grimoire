using Grimoire.Core;

namespace Grimoire.Api.Contracts;

public sealed record CreateMemoryRequest
{
    public required string Content { get; init; }

    public required MemoryScope Scope { get; init; }

    public MemoryKind Kind { get; init; } = MemoryKind.Journal;

    public string? ProjectId { get; init; }

    public string? SessionId { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public double Importance { get; init; } = 0.5;

    public IReadOnlyList<Guid> Sources { get; init; } = [];
}
