using Grimoire.Core;
using ModelContextProtocol.Server;
using Grimoire.Mcp.Tools;

namespace Grimoire.Mcp;

internal static class ToolCatalog
{
    public static IReadOnlyList<McpServerTool> CreateTools(McpHostOptions options, IServiceProvider services, ClientInfoHolder clientInfo)
    {
        var store = Require<IMemoryStore>(services);
        var embeddings = Require<IEmbeddingProvider>(services);
        var clock = Require<IClock>(services);
        var engineOptions = services.GetService(typeof(MemoryEngineOptions)) as MemoryEngineOptions;

        var memories = new MemoryService(store, embeddings, clock, engineOptions);
        var persona = new PersonaService(store, embeddings, clock);
        var brief = new BriefService(store, persona, clock, engineOptions);
        var claims = Require<IActorClaimProvider>(services);
        var actors = new MemoryActorProvider(
            options.Actor,
            options.EnvTome,
            options.EnvActor,
            clientInfo,
            claims,
            new TomeResolver(engineOptions));

        return
        [
            Build(RememberTool.Name, RememberTool.Title, RememberTool.Description, new RememberTool(memories, actors).Remember, readOnly: false, destructive: false, idempotent: false),
            Build(RecallTool.Name, RecallTool.Title, RecallTool.Description, new RecallTool(memories, actors).Recall, readOnly: true, destructive: false, idempotent: true),
            Build(ForgetTool.Name, ForgetTool.Title, ForgetTool.Description, new ForgetTool(memories, actors).Forget, readOnly: false, destructive: true, idempotent: true),
            Build(ListTool.Name, ListTool.Title, ListTool.Description, new ListTool(memories, actors).List, readOnly: true, destructive: false, idempotent: true),
            Build(PersonaGetTool.Name, PersonaGetTool.Title, PersonaGetTool.Description, new PersonaGetTool(persona, actors).Get, readOnly: true, destructive: false, idempotent: true),
            Build(BriefTool.Name, BriefTool.Title, BriefTool.Description, new BriefTool(brief, actors).Brief, readOnly: true, destructive: false, idempotent: true),
            Build(CompactCandidatesTool.Name, CompactCandidatesTool.Title, CompactCandidatesTool.Description, new CompactCandidatesTool(memories, store, actors).GetCandidates, readOnly: true, destructive: false, idempotent: true),
            Build(StatsTool.Name, StatsTool.Title, StatsTool.Description, new StatsTool(memories, actors).GetStats, readOnly: true, destructive: false, idempotent: true),
            Build(ScryTool.Name, ScryTool.Title, ScryTool.Description, new ScryTool(memories, store, actors).Scry, readOnly: true, destructive: false, idempotent: true),
        ];
    }

    private static McpServerTool Build(string name, string title, string description, Delegate handler, bool readOnly, bool destructive, bool idempotent)
    {
        return McpServerTool.Create(handler, new McpServerToolCreateOptions
        {
            Name = name,
            Title = title,
            Description = description,
            ReadOnly = readOnly,
            Destructive = destructive,
            Idempotent = idempotent,
            OpenWorld = false,
        });
    }

    private static T Require<T>(IServiceProvider services) where T : class
    {
        return services.GetService(typeof(T)) as T
            ?? throw new InvalidOperationException($"Grimoire MCP host requires {typeof(T).Name} in the service provider.");
    }
}
