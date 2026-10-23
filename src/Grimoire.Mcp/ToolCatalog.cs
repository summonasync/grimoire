using System.Text.Json.Nodes;
using Grimoire.Core;
using Microsoft.Extensions.AI;
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
        var gcOptions = services.GetService(typeof(GcOptions)) as GcOptions;
        var gcState = services.GetService(typeof(IGcStateStore)) as IGcStateStore;

        var memories = new MemoryService(store, embeddings, clock, engineOptions, gcOptions, gcState);
        var persona = new PersonaService(store, embeddings, clock);
        var brief = new BriefService(store, persona, clock, engineOptions);
        var corrections = new CorrectionService(store, clock, engineOptions);
        var callerPaths = Require<ICallerPathProvider>(services);
        var claims = Require<IActorClaimProvider>(services);
        var registryStore = Require<IProjectRegistryStore>(services);
        var projects = new LiveProjectRegistry(registryStore);
        var projectTags = new ProjectTagResolver(projects);
        var projectSync = new ProjectSyncService(Require<IBoardProjectSource>(services), registryStore);
        var actors = new MemoryActorProvider(
            options.Actor,
            options.EnvTome,
            options.EnvActor,
            clientInfo,
            claims,
            new TomeResolver(engineOptions));

        return
        [
            Build(RememberTool.Name, RememberTool.Title, RememberTool.Description, new RememberTool(memories, actors, projectTags, callerPaths).Remember, readOnly: false, destructive: false, idempotent: false),
            Build(RecallTool.Name, RecallTool.Title, RecallTool.Description, new RecallTool(memories, actors, projects, callerPaths).Recall, readOnly: true, destructive: false, idempotent: true),
            Build(ForgetTool.Name, ForgetTool.Title, ForgetTool.Description, new ForgetTool(memories, actors).Forget, readOnly: false, destructive: true, idempotent: true),
            Build(RestoreTool.Name, RestoreTool.Title, RestoreTool.Description, new RestoreTool(corrections, store, actors).Restore, readOnly: false, destructive: false, idempotent: true),
            Build(HistoryTool.Name, HistoryTool.Title, HistoryTool.Description, new HistoryTool(corrections, actors).History, readOnly: true, destructive: false, idempotent: true),
            Build(ListTool.Name, ListTool.Title, ListTool.Description, new ListTool(memories, actors).List, readOnly: true, destructive: false, idempotent: true),
            Build(PersonaGetTool.Name, PersonaGetTool.Title, PersonaGetTool.Description, new PersonaGetTool(persona, actors).Get, readOnly: true, destructive: false, idempotent: true),
            Build(BriefTool.Name, BriefTool.Title, BriefTool.Description, new BriefTool(brief, actors).Brief, readOnly: true, destructive: false, idempotent: true),
            Build(CompactCandidatesTool.Name, CompactCandidatesTool.Title, CompactCandidatesTool.Description, new CompactCandidatesTool(memories, store, actors).GetCandidates, readOnly: true, destructive: false, idempotent: true),
            Build(StatsTool.Name, StatsTool.Title, StatsTool.Description, new StatsTool(memories, actors).GetStats, readOnly: true, destructive: false, idempotent: true),
            Build(ScryTool.Name, ScryTool.Title, ScryTool.Description, new ScryTool(memories, store, actors).Scry, readOnly: true, destructive: false, idempotent: true),
            Build(ProjectsTool.Name, ProjectsTool.Title, ProjectsTool.Description, new ProjectsTool(registryStore, projectSync).Projects, readOnly: false, destructive: true, idempotent: false),
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
            SchemaCreateOptions = new AIJsonSchemaCreateOptions
            {
                TransformSchemaNode = MoveArrayEnumToItems,
            },
        });
    }

    private static JsonNode MoveArrayEnumToItems(AIJsonSchemaCreateContext context, JsonNode node)
    {
        if (node is not JsonObject obj
            || !obj.TryGetPropertyValue("enum", out var enumNode)
            || enumNode is not JsonArray tokens
            || !obj.TryGetPropertyValue("items", out _))
        {
            return node;
        }

        obj.Remove("enum");
        obj["items"] = new JsonObject
        {
            ["type"] = "string",
            ["enum"] = (JsonArray)tokens.DeepClone(),
        };
        return obj;
    }

    private static T Require<T>(IServiceProvider services) where T : class
    {
        return services.GetService(typeof(T)) as T
            ?? throw new InvalidOperationException($"Grimoire MCP host requires {typeof(T).Name} in the service provider.");
    }
}
