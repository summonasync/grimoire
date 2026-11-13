namespace Grimoire.Infrastructure.Configuration;

public sealed record QdrantOptions
{
    public string Endpoint { get; init; } = "http://localhost:6334";

    public string? ApiKey { get; init; }

    public string Collection { get; init; } = "grimoire";

    public string? ManifestCollection { get; init; }

    public string ManifestCollectionOrDefault => ManifestCollection ?? $"{Collection}-manifests";

    public string? SessionCollection { get; init; }

    public string SessionCollectionOrDefault => SessionCollection ?? $"{Collection}-sessions";
}
