using ModelContextProtocol.Client;

namespace Grimoire.Mcp.Tests;

internal static class McpClientExtensions
{
    public static async Task<IReadOnlyDictionary<string, McpClientTool>> ListToolMap(this McpClient client)
    {
        var tools = await client.ListToolsAsync();
        return tools.ToDictionary(tool => tool.Name, StringComparer.Ordinal);
    }

    public static async Task<string[]> ListToolNames(this McpClient client)
    {
        var tools = await client.ListToolsAsync();
        return [.. tools.Select(tool => tool.Name)];
    }
}
