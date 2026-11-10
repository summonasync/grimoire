using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Grimoire.Infrastructure.Board;
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
        IConsolidationStateStore? consolidationState = null,
        string? configPath = null,
        ICallerPathProvider? callerPaths = null,
        IReadOnlyList<ProjectAliasRecord>? projects = null,
        FakeBoardProjectSource? board = null)
    {
        var vectorDb = db ?? new RecordingVectorDb();
        var services = new ServiceCollection();
        var registryStore = new ProjectRegistryFileStore(configPath);
        Seed(registryStore, projects, configPath);
        services.AddSingleton<IProjectRegistryStore>(registryStore);
        services.AddSingleton<IProjectRegistry>(new LiveProjectRegistry(registryStore));
        services.AddSingleton<ProjectTagResolver>();
        services.AddSingleton<IBoardProjectSource>(board ?? new FakeBoardProjectSource());
        services.AddSingleton<ProjectSyncService>();
        services.AddSingleton<IQdrantVectorDb>(vectorDb);
        services.AddSingleton(new QdrantCollectionBootstrapper(vectorDb, "grimoire"));
        services.AddSingleton(sp => new EmbeddingReindexer(
            sp.GetRequiredService<IQdrantVectorDb>(),
            "grimoire",
            sp.GetRequiredService<QdrantCollectionBootstrapper>(),
            sp.GetRequiredService<IEmbeddingProvider>(),
            sp.GetRequiredService<MemoryEngineOptions>().ReindexBatchSize));
        services.AddSingleton<IMemoryStore>(store);
        services.AddSingleton<ITomeManifestStore>(new FakeTomeManifestStore());
        services.AddSingleton<IEmbeddingProvider>(embeddings);
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IActorClaimProvider>(claims ?? new NullClaimProvider());
        services.AddSingleton<ICallerPathProvider>(callerPaths ?? new FixedCallerPath("/agents/sandbox"));
        services.AddSingleton(options ?? TunedOptions());
        services.AddSingleton(gcOptions ?? new GcOptions());
        services.AddSingleton(gcState ?? new FakeGcStateStore());
        services.AddSingleton(new ConsolidationOptions());
        services.AddSingleton(consolidationState ?? new FakeConsolidationStateStore());
        services.AddSingleton(sp => new MemoryService(
            sp.GetRequiredService<IMemoryStore>(),
            sp.GetRequiredService<IEmbeddingProvider>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<MemoryEngineOptions>(),
            sp.GetRequiredService<GcOptions>(),
            sp.GetRequiredService<IGcStateStore>(),
            consolidationOptions: sp.GetRequiredService<ConsolidationOptions>(),
            consolidationStateStore: sp.GetRequiredService<IConsolidationStateStore>()));
        services.AddSingleton(sp => new PersonaService(
            sp.GetRequiredService<IMemoryStore>(),
            sp.GetRequiredService<IEmbeddingProvider>(),
            sp.GetRequiredService<IClock>()));
        services.AddSingleton(sp => new BriefService(
            sp.GetRequiredService<IMemoryStore>(),
            sp.GetRequiredService<PersonaService>(),
            sp.GetRequiredService<ITomeManifestStore>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<MemoryEngineOptions>()));
        services.AddSingleton<AutoMemoryImporter>();
        return services.BuildServiceProvider();
    }

    public static MemoryEngineOptions TunedOptions() => new()
    {
        SessionTtlHours = 12,
        DedupSimilarityThreshold = 0.86,
        TombstoneRetentionDays = 7,
    };

    private static void Seed(ProjectRegistryFileStore store, IReadOnlyList<ProjectAliasRecord>? projects, string? configPath)
    {
        if (projects is null || projects.Count == 0 || configPath is null)
        {
            return;
        }

        store.Mutate(records =>
        {
            records.AddRange(projects);
            return "seeded for tests";
        });
    }

    private sealed class NullClaimProvider : IActorClaimProvider
    {
        public Task<ActorClaim?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult<ActorClaim?>(null);
    }
}
