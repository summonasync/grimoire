namespace Grimoire.Infrastructure.Configuration;

public sealed record QdrantOptions
{
    public string Endpoint { get; init; } = "http://localhost:6334";

    public string? ApiKey { get; init; }

    public string Collection { get; init; } = "grimoire";
}
