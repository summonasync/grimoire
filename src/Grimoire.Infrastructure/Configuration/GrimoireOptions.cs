using Grimoire.Core;

namespace Grimoire.Infrastructure.Configuration;

public sealed record GrimoireOptions
{
    public string DefaultTome { get; init; } = TomeKey.DefaultTome;

    public QdrantOptions Qdrant { get; init; } = new();

    public EmbeddingOptions Embedding { get; init; } = new();

    public MemoryOptions Memory { get; init; } = new();

    public ServeOptions Serve { get; init; } = new();
}
