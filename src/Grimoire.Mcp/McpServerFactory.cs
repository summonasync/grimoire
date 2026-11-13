using Grimoire.Core;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Grimoire.Mcp;

internal static class McpServerFactory
{
    public static McpServer Create(ITransport transport, McpHostOptions options, IServiceProvider services)
    {
        var clientInfo = new ClientInfoHolder();
        var sessionIdentity = new SessionIdentityHolder();
        var actors = ActorProviders.Create(options, services, clientInfo, sessionIdentity);
        var serverOptions = new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = options.ServerName,
                Title = options.ServerTitle,
                Version = options.ServerVersion,
            },
            ServerInstructions = options.Instructions,
            Capabilities = new ServerCapabilities { Tools = new ToolsCapability() },
            ToolCollection = new McpServerPrimitiveCollection<McpServerTool>(),
        };

        foreach (var tool in ToolCatalog.CreateTools(services, sessionIdentity, actors))
        {
            serverOptions.ToolCollection.Add(tool);
        }

        var capture = new SessionCapture(
            Require<ISessionRegistry>(services),
            actors,
            Require<IClock>(services),
            options.CaptureFault ?? new StderrSessionCaptureFaultSink(),
            SessionHandshakeRow.StdioTransport,
            options.SessionWriteBudget ?? SessionCapture.DefaultWriteBudget);
        serverOptions.Filters.Message.IncomingFilters.Add(capture.Wrap);

        var server = McpServer.Create(transport, serverOptions, NullLoggerFactory.Instance, services);
        clientInfo.Bind(server);
        capture.Bind(server);
        return server;
    }

    private static T Require<T>(IServiceProvider services) where T : class =>
        services.GetService(typeof(T)) as T
        ?? throw new InvalidOperationException($"Grimoire MCP host requires {typeof(T).Name} in the service provider.");
}
