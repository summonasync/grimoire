using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp;

internal static class ToolResults
{
    public static async Task<CallToolResult> ExecuteAsync(Func<CancellationToken, Task<CallToolResult>> action, CancellationToken cancellationToken)
    {
        try
        {
            return await action(cancellationToken);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Failure(ToolErrorMapper.Map(exception));
        }
    }

    public static CallToolResult Success<T>(T payload) where T : notnull
    {
        var element = JsonSerializer.SerializeToElement(payload, McpJsonUtilities.DefaultOptions);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = element.GetRawText() }],
            StructuredContent = element,
        };
    }

    public static CallToolResult Failure(ToolError error)
    {
        var element = JsonSerializer.SerializeToElement(new ErrorEnvelope(error), McpJsonUtilities.DefaultOptions);
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = element.GetRawText() }],
            StructuredContent = element,
        };
    }

    private sealed record ErrorEnvelope(ToolError Error);
}
