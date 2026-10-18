namespace Grimoire.Api.Contracts;

public sealed record RestoreMemoryRequest
{
    public required string Reason { get; init; }

    public string? Tome { get; init; }
}
