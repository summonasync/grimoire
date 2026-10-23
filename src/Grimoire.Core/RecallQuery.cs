namespace Grimoire.Core;

public sealed record RecallQuery
{
    public required string Text { get; init; }

    public IReadOnlyList<MemoryScope> Scopes { get; init; } = [];

    public MemoryKind? Kind { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string? Tome { get; init; }

    public IReadOnlyList<string> Tomes { get; init; } = [];

    public string? ProjectId { get; init; }

    public string? CallerProjectId { get; init; }

    public string? SessionId { get; init; }

    public int Limit { get; init; } = 5;
}
