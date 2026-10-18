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
        RecordingVectorDb? db = null,
        IActorClaimProvider? claims = null,
        GcOptions? gcOptions = null,
        IGcStateStore? gcState = null)
    {
        var vectorDb = db ?? new RecordingVectorDb();
        var services = new ServiceCollection();
        services.AddSingleton<IQdrantVectorDb>(vectorDb);
        services.AddSingleton(new QdrantCollectionBootstrapper(vectorDb, "grimoire"));
        services.AddSingleton(sp => new EmbeddingReindexer(
            sp.GetRequiredService<IQdrantVectorDb>(),
            "grimoire",
            sp.GetRequiredService<QdrantCollectionBootstrapper>(),
            sp.GetRequiredService<IEmbeddingProvider>(),
            sp.GetRequiredService<MemoryEngineOptions>().ReindexBatchSize));
        services.AddSingleton<IMemoryStore>(store);
        services.AddSingleton<IEmbeddingProvider>(embeddings);
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IActorClaimProvider>(claims ?? new NullClaimProvider());
        services.AddSingleton(options ?? TunedOptions());
        services.AddSingleton(gcOptions ?? new GcOptions());
        services.AddSingleton(gcState ?? new FakeGcStateStore());
        services.AddSingleton(sp => new MemoryService(
            sp.GetRequiredService<IMemoryStore>(),
            sp.GetRequiredService<IEmbeddingProvider>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<MemoryEngineOptions>(),
            sp.GetRequiredService<GcOptions>(),
            sp.GetRequiredService<IGcStateStore>()));
        services.AddSingleton(sp => new PersonaService(
            sp.GetRequiredService<IMemoryStore>(),
            sp.GetRequiredService<IEmbeddingProvider>(),
            sp.GetRequiredService<IClock>()));
        services.AddSingleton(sp => new BriefService(
            sp.GetRequiredService<IMemoryStore>(),
            sp.GetRequiredService<PersonaService>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<MemoryEngineOptions>()));
        return services.BuildServiceProvider();
    }

    public static MemoryEngineOptions TunedOptions() => new()
    {
        SessionTtlHours = 12,
        DedupSimilarityThreshold = 0.86,
        TombstoneRetentionDays = 7,
    };

    private sealed class NullClaimProvider : IActorClaimProvider
    {
        public Task<ActorClaim?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult<ActorClaim?>(null);
    }
}
