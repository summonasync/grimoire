namespace Grimoire.Core;

public sealed record MemoryFilter
{
    public IReadOnlyList<MemoryScope> Scopes { get; init; } = [];

    public MemoryKind? Kind { get; init; }

    public MemoryStatus? Status { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<string> ExcludeTags { get; init; } = [];

    public string? ProjectId { get; init; }

    public string? SessionId { get; init; }

    public DateTimeOffset? ExpiresBefore { get; init; }

    public DateTimeOffset? TombstonedBefore { get; init; }

    public int? Limit { get; init; }

    public string? IdempotencyKey { get; init; }

    public DateTimeOffset? CreatedAfter { get; init; }

    public bool IsEmpty =>
        Scopes.Count == 0 &&
        Kind is null &&
        Status is null &&
        Tags.Count == 0 &&
        ExcludeTags.Count == 0 &&
        ProjectId is null &&
        SessionId is null &&
        ExpiresBefore is null &&
        TombstonedBefore is null &&
        IdempotencyKey is null &&
        CreatedAfter is null;
}
