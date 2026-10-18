using Grimoire.Core;

namespace Grimoire.Mcp.Projections;

internal sealed record RememberView
{
    public required Guid MemoryId { get; init; }

    public required MemoryScope Scope { get; init; }

    public required MemoryKind Kind { get; init; }

    public required string Actor { get; init; }

    public string? ProjectId { get; init; }

    public string? SessionId { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public required bool SupersededExisting { get; init; }

    public Guid? SupersededMemoryId { get; init; }

    public required bool IdempotentReplay { get; init; }

    public required bool MaintenancePending { get; init; }

    public required int BodyChars { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
