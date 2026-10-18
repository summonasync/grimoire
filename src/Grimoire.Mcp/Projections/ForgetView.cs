namespace Grimoire.Mcp.Projections;

internal sealed record ForgetView
{
    public required Guid MemoryId { get; init; }

    public required string Tome { get; init; }

    public required bool Forgotten { get; init; }

    public string? Reason { get; init; }
}
