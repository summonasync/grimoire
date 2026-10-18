namespace Grimoire.Api.Contracts;

public sealed record HealthResponse(string Status, ComponentHealth Store, ComponentHealth Embeddings, DateTimeOffset CheckedAt)
{
    public const string Healthy = "healthy";

    public const string Degraded = "degraded";
}
