namespace Grimoire.Core;

public sealed record HistoryResult
{
    public required Guid Id { get; init; }

    public bool Found { get; init; }

    public IReadOnlyList<HistoryEntry> Chain { get; init; } = [];
}
