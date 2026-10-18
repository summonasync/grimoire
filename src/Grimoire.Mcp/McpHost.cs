using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;

namespace Grimoire.Mcp;

public static class McpHost
{
    public static async Task<int> RunAsync(McpHostOptions options, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(services);

        await using var transport = new StdioServerTransport(options.ServerName, NullLoggerFactory.Instance);
        await using var server = McpServerFactory.Create(transport, options, services);
        try
        {
            await server.RunAsync(cancellationToken);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync($"Grimoire MCP host terminated unexpectedly: {exception.Message}");
            return 1;
        }
    }
}
