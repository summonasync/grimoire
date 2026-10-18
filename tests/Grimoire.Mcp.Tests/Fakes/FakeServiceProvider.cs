using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

internal sealed class FakeServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, object> _services;

    public FakeServiceProvider(
        IMemoryStore store,
        IEmbeddingProvider embeddings,
        IClock clock,
        MemoryEngineOptions? engineOptions = null,
        IActorClaimProvider? claims = null,
        GcOptions? gcOptions = null,
        IGcStateStore? gcState = null)
    {
        _services = new Dictionary<Type, object>
        {
            [typeof(IMemoryStore)] = store,
            [typeof(IEmbeddingProvider)] = embeddings,
            [typeof(IClock)] = clock,
            [typeof(IActorClaimProvider)] = claims ?? new FakeActorClaimProvider(),
            [typeof(GcOptions)] = gcOptions ?? new GcOptions(),
            [typeof(IGcStateStore)] = gcState ?? new FakeGcStateStore(),
        };

        if (engineOptions is not null)
        {
            _services[typeof(MemoryEngineOptions)] = engineOptions;
        }
    }

    public object? GetService(Type serviceType) => _services.TryGetValue(serviceType, out var service) ? service : null;
}
