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

        foreach (var tool in ToolCatalog.CreateTools(options, services, clientInfo, sessionIdentity))
        {
            serverOptions.ToolCollection.Add(tool);
        }

        var server = McpServer.Create(transport, serverOptions, NullLoggerFactory.Instance, services);
        clientInfo.Bind(server);
        return server;
    }
}
