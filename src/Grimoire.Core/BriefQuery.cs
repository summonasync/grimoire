namespace Grimoire.Core;

public sealed record BriefQuery
{
    public string? Tome { get; init; }

    public string? ProjectId { get; init; }

    public IReadOnlyList<MemoryScope> Scopes { get; init; } = [];

    public int? FactsPerScope { get; init; }

    public int? JournalPerScope { get; init; }

    public int? TombstoneLimit { get; init; }

    public bool Daily { get; init; }
}
