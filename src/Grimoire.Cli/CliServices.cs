using Grimoire.Core;
using Grimoire.Infrastructure.Autostart;
using Grimoire.Infrastructure.Board;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Embeddings;
using Grimoire.Infrastructure.Identity;
using Grimoire.Infrastructure.Memory;
using Grimoire.Infrastructure.Network;
using Grimoire.Infrastructure.Processes;
using Grimoire.Infrastructure.Tomes;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli;

internal static class CliServices
{
    public static ServiceProvider Build(GrimoireOptions options, string? configPath = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IProjectRegistryStore>(new ProjectRegistryFileStore(configPath));
        services.AddSingleton<IProjectRegistry>(sp => new LiveProjectRegistry(sp.GetRequiredService<IProjectRegistryStore>()));
        services.AddSingleton<ProjectTagResolver>();
        services.AddSingleton<IBoardProjectSource>(sp => new OvermindBoardProjectSource(sp.GetRequiredService<HttpClient>()));
        services.AddSingleton<ProjectSyncService>();
        services.AddSingleton<IQdrantVectorDb>(_ => new QdrantVectorDb(options.Qdrant.Endpoint, options.Qdrant.ApiKey));
        services.AddSingleton(sp => new QdrantCollectionBootstrapper(
            sp.GetRequiredService<IQdrantVectorDb>(),
            options.Qdrant.Collection));
        services.AddSingleton(sp => new EmbeddingReindexer(
            sp.GetRequiredService<IQdrantVectorDb>(),
            options.Qdrant.Collection,
            sp.GetRequiredService<QdrantCollectionBootstrapper>(),
            sp.GetRequiredService<IEmbeddingProvider>(),
            sp.GetRequiredService<MemoryEngineOptions>().ReindexBatchSize));
        services.AddSingleton<IMemoryStore>(sp => new QdrantMemoryStore(
            sp.GetRequiredService<IQdrantVectorDb>(),
            options.Qdrant.Collection,
            options.Embedding));
        services.AddSingleton<ITomeManifestStore>(sp => new QdrantManifestStore(
            sp.GetRequiredService<IQdrantVectorDb>(),
            options.Qdrant));
        services.AddSingleton<TomeManifestService>();
        services.AddSingleton<TomePurgeService>();
        services.AddSingleton<HttpClient>(_ => new HttpClient());
        services.AddSingleton<IEmbeddingProvider>(sp => new OllamaEmbeddingProvider(
            sp.GetRequiredService<HttpClient>(),
            options.Embedding));
        services.AddSingleton<IClock>(SystemClock.Instance);
        services.AddSingleton(EngineOptionsMapper.ToEngineOptions(options.Memory, options.DefaultTome));
        services.AddSingleton(new GcOptions { SweepInterval = TimeSpan.FromMinutes(options.Serve.GcIntervalMinutes) });
        services.AddSingleton<IGcStateStore>(new GcStateFileStore());
        services.AddSingleton(new ConsolidationOptions());
        services.AddSingleton<IConsolidationStateStore>(new ConsolidationStateFileStore());
        services.AddSingleton<IActorClaimProvider, ActorClaimProvider>();
        services.AddSingleton<IAgentIdentitySource, AgentIdentityFileReader>();
        services.AddSingleton<IExternalProcess, SystemProcess>();
        services.AddSingleton<ICallerPathProvider, ProcessCallerPathProvider>();
        services.AddSingleton<IConsoleProbe>(sp => new LoopbackConsoleProbe(sp.GetRequiredService<HttpClient>()));
        services.AddSingleton<IHostNameResolver, SystemHostNameResolver>();
        services.AddSingleton<IAutostartEntryWriter, AutostartEntryWriter>();
        services.AddSingleton(Grimoire.Cli.Commands.UiTimings.Default);
        services.AddSingleton<MemoryService>(sp => new MemoryService(
            sp.GetRequiredService<IMemoryStore>(),
            sp.GetRequiredService<IEmbeddingProvider>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<MemoryEngineOptions>(),
            sp.GetRequiredService<GcOptions>(),
            sp.GetRequiredService<IGcStateStore>(),
            consolidationOptions: sp.GetRequiredService<ConsolidationOptions>(),
            consolidationStateStore: sp.GetRequiredService<IConsolidationStateStore>()));
        services.AddSingleton<PersonaService>(sp => new PersonaService(
            sp.GetRequiredService<IMemoryStore>(),
            sp.GetRequiredService<IEmbeddingProvider>(),
            sp.GetRequiredService<IClock>()));
        services.AddSingleton<BriefService>(sp => new BriefService(
            sp.GetRequiredService<IMemoryStore>(),
            sp.GetRequiredService<PersonaService>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<MemoryEngineOptions>()));
        services.AddSingleton<AutoMemoryImporter>();
        return services.BuildServiceProvider();
    }
}
