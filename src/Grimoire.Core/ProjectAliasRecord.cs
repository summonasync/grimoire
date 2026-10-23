namespace Grimoire.Core;

public sealed record ProjectAliasRecord
{
    public required string Alias { get; init; }

    public IReadOnlyList<string> Paths { get; init; } = [];

    public string? BoardProjectId { get; init; }

    public string? DisplayName { get; init; }
}
