using Grimoire.Core;

namespace Grimoire.Mcp;

internal static class ActorProviders
{
    public static MemoryActorProvider Create(
        McpHostOptions options,
        IServiceProvider services,
        ClientInfoHolder clientInfo,
        SessionIdentityHolder sessionIdentity) =>
        new(
            options.Actor,
            options.EnvTome,
            options.EnvActor,
            sessionIdentity,
            options.IdentityFile ?? new UnsetAgentIdentitySource(),
            clientInfo,
            Require<IActorClaimProvider>(services),
            new TomeResolver(EngineOptions(services)));

    private static MemoryEngineOptions? EngineOptions(IServiceProvider services) =>
        services.GetService(typeof(MemoryEngineOptions)) as MemoryEngineOptions;

    private static T Require<T>(IServiceProvider services) where T : class =>
        services.GetService(typeof(T)) as T
        ?? throw new InvalidOperationException($"Grimoire MCP host requires {typeof(T).Name} in the service provider.");
}
