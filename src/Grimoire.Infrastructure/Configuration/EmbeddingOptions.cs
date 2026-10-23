namespace Grimoire.Infrastructure.Configuration;

public sealed record EmbeddingOptions
{
    public string Provider { get; init; } = "ollama";

    public string Model { get; init; } = "qwen3-embedding";

    public string Endpoint { get; init; } = "http://localhost:11434";

    public int Dimensions { get; init; } = 1024;
}
