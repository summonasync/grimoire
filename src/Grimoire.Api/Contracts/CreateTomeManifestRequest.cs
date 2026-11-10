namespace Grimoire.Api.Contracts;

public sealed record CreateTomeManifestRequest
{
    public string? Key { get; init; }

    public string? Description { get; init; }
}
