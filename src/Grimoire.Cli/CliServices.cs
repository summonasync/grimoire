using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Embeddings;
using Grimoire.Infrastructure.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli;

internal static class CliServices
{
    public static ServiceProvider Build(GrimoireOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IQdrantVectorDb>(_ => new QdrantVectorDb(options.Qdrant.Endpoint, options.Qdrant.ApiKey));
        services.AddSingleton(sp => new QdrantCollectionBootstrapper(
            sp.GetRequiredService<IQdrantVectorDb>(),
            options.Qdrant.Collection));
        services.AddSingleton<IMemoryStore>(sp => new QdrantMemoryStore(
            sp.GetRequiredService<IQdrantVectorDb>(),
            options.Qdrant.Collection,
            options.Embedding));
        services.AddSingleton<HttpClient>(_ => new HttpClient());
        services.AddSingleton<IEmbeddingProvider>(sp => new OllamaEmbeddingProvider(
            sp.GetRequiredService<HttpClient>(),
            options.Embedding));
        services.AddSingleton<IClock>(SystemClock.Instance);
        services.AddSingleton(EngineOptionsMapper.ToEngineOptions(options.Memory));
        services.AddSingleton<MemoryService>(sp => new MemoryService(
            sp.GetRequiredService<IMemoryStore>(),
            sp.GetRequiredService<IEmbeddingProvider>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<MemoryEngineOptions>()));
        services.AddSingleton<PersonaService>(sp => new PersonaService(
            sp.GetRequiredService<IMemoryStore>(),
            sp.GetRequiredService<IEmbeddingProvider>(),
            sp.GetRequiredService<IClock>()));
        return services.BuildServiceProvider();
    }
}
