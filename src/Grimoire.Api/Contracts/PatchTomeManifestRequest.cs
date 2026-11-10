namespace Grimoire.Api.Contracts;

public sealed record PatchTomeManifestRequest
{
    public string? Description { get; init; }
}
