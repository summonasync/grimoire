namespace Grimoire.Mcp.Projections;

internal sealed record TomeCreateView
{
    public required string Tome { get; init; }

    public required string Description { get; init; }

    public required string CreatedBy { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
