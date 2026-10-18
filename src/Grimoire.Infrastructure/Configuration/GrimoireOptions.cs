namespace Grimoire.Infrastructure.Configuration;

public sealed record GrimoireOptions
{
    public QdrantOptions Qdrant { get; init; } = new();

    public EmbeddingOptions Embedding { get; init; } = new();

    public MemoryOptions Memory { get; init; } = new();

    public ServeOptions Serve { get; init; } = new();
}
