using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Tests;

internal static class TestServices
{
    public static readonly IReadOnlyList<ProjectAliasRecord> DefaultProjects =
    [
        new ProjectAliasRecord { Alias = "sandbox", Paths = ["/agents/sandbox"] },
        new ProjectAliasRecord { Alias = "grimoire", Paths = ["/src/grimoire"] },
    ];

    public static ServiceProvider Build(
        RecordingMemoryStore store,
        StubEmbeddings embeddings,
        FakeClock clock,
        MemoryEngineOptions? options = null,
        RecordingVectorDb? db = null,
        IActorClaimProvider? claims = null,
        GcOptions? gcOptions = null,
        IGcStateStore? gcState = null,
        string? configPath = null,
        ICallerPathProvider? callerPaths = null,
        IReadOnlyList<ProjectAliasRecord>? projects = null)
    {
        var vectorDb = db ?? new RecordingVectorDb();
        var services = new ServiceCollection();
        services.AddSingleton<IProjectRegistryStore>(new ProjectRegistryFileStore(configPath));
        services.AddSingleton<IProjectRegistry>(new ProjectRegistry(projects ?? DefaultProjects));
        services.AddSingleton<ProjectTagResolver>();
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
        services.AddSingleton<ICallerPathProvider>(callerPaths ?? new FixedCallerPath("/agents/sandbox"));
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
