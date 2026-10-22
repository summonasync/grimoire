using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;

namespace Grimoire.Mcp;

public static class McpHost
{
    public static async Task<int> RunAsync(McpHostOptions options, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(services);

        try
        {
            await using var transport = new StdioServerTransport(options.ServerName, NullLoggerFactory.Instance);
            await using var server = McpServerFactory.Create(transport, options, services);
            await Console.Error.WriteLineAsync(BootMarkers.MountOkLine(options.ServerName, options.ServerVersion));
            await server.RunAsync(cancellationToken);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception exception)
        {
            var reason = BootMarkers.ClassifyHostException(exception);
            await Console.Error.WriteLineAsync(BootMarkers.MountFailLine(reason, exception.Message));
            return 1;
        }
    }
}
