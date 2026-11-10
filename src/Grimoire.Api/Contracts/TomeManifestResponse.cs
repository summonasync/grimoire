namespace Grimoire.Api.Contracts;

public sealed record TomeManifestResponse(
    string Key,
    string Description,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
