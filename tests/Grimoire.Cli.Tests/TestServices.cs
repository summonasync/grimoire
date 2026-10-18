using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Grimoire.Infrastructure.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Tests;

internal static class TestServices
{
    public static ServiceProvider Build(
        RecordingMemoryStore store,
        StubEmbeddings embeddings,
        FakeClock clock,
        MemoryEngineOptions? options = null,
        RecordingVectorDb? db = null)
    {
        var vectorDb = db ?? new RecordingVectorDb();
        var services = new ServiceCollection();
        services.AddSingleton<IQdrantVectorDb>(vectorDb);
        services.AddSingleton(new QdrantCollectionBootstrapper(vectorDb, "grimoire"));
        services.AddSingleton<IMemoryStore>(store);
        services.AddSingleton<IEmbeddingProvider>(embeddings);
        services.AddSingleton<IClock>(clock);
        services.AddSingleton(options ?? TunedOptions());
        services.AddSingleton(sp => new MemoryService(
            sp.GetRequiredService<IMemoryStore>(),
            sp.GetRequiredService<IEmbeddingProvider>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<MemoryEngineOptions>()));
        services.AddSingleton(sp => new PersonaService(
            sp.GetRequiredService<IMemoryStore>(),
            sp.GetRequiredService<IEmbeddingProvider>(),
            sp.GetRequiredService<IClock>()));
        return services.BuildServiceProvider();
    }

    public static MemoryEngineOptions TunedOptions() => new()
    {
        SessionTtlHours = 12,
        DedupSimilarityThreshold = 0.86,
        TombstoneRetentionDays = 7,
    };
}
