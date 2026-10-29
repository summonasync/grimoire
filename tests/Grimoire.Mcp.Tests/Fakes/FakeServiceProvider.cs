using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

internal sealed class FakeServiceProvider : IServiceProvider
{
    public const string DefaultCallerPath = "/agents/sandbox";

    private readonly Dictionary<Type, object> _services;

    public FakeServiceProvider(
        IMemoryStore store,
        IEmbeddingProvider embeddings,
        IClock clock,
        MemoryEngineOptions? engineOptions = null,
        IActorClaimProvider? claims = null,
        GcOptions? gcOptions = null,
        IGcStateStore? gcState = null,
        ICallerPathProvider? callerPaths = null,
        IProjectRegistryStore? registryStore = null,
        IBoardProjectSource? board = null,
        IConsolidationStateStore? consolidationState = null,
        IAgentIdentitySource? identityFile = null)
    {
        IProjectRegistryStore effectiveRegistryStore = registryStore ?? new FakeProjectRegistryStore();
        _services = new Dictionary<Type, object>
        {
            [typeof(IMemoryStore)] = store,
            [typeof(IEmbeddingProvider)] = embeddings,
            [typeof(IClock)] = clock,
            [typeof(IActorClaimProvider)] = claims ?? new FakeActorClaimProvider(),
            [typeof(GcOptions)] = gcOptions ?? new GcOptions(),
            [typeof(IGcStateStore)] = gcState ?? new FakeGcStateStore(),
            [typeof(ConsolidationOptions)] = new ConsolidationOptions(),
            [typeof(IConsolidationStateStore)] = consolidationState ?? new FakeConsolidationStateStore(),
            [typeof(ICallerPathProvider)] = callerPaths ?? new FakeCallerPathProvider(DefaultCallerPath),
            [typeof(IProjectRegistryStore)] = effectiveRegistryStore,
            [typeof(IProjectRegistry)] = new LiveProjectRegistry(effectiveRegistryStore),
            [typeof(IBoardProjectSource)] = board ?? new FakeBoardProjectSource(),
        };

        if (engineOptions is not null)
        {
            _services[typeof(MemoryEngineOptions)] = engineOptions;
        }

        if (identityFile is not null)
        {
            _services[typeof(IAgentIdentitySource)] = identityFile;
        }
    }

    public object? GetService(Type serviceType) => _services.TryGetValue(serviceType, out var service) ? service : null;
}
