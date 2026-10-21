using System.Text.Encodings.Web;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp;

internal static class ToolResults
{
    // UnsafeRelaxedJsonEscaping leaves <, >, & and ' unescaped — the HTML-script-injection
    // surface this encoder exists to guard. Accepted tradeoff: the text block is delivered to
    // an MCP client as JSON text and never rendered into an HTML document, while the default
    // HTML-safe encoder costs six wire characters per Cyrillic letter (measured 352 escape
    // sequences on one crew record). Quotes, backslashes and control characters are still
    // escaped by the encoder itself. Do not "fix" this back — see PayloadEncoderContractTests.
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(McpJsonUtilities.DefaultOptions)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

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
        var element = JsonSerializer.SerializeToElement(payload, ResponseJsonOptions);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = element.GetRawText() }],
        };
    }

    public static CallToolResult Failure(ToolError error)
    {
        var element = JsonSerializer.SerializeToElement(new ErrorEnvelope(error), ResponseJsonOptions);
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = element.GetRawText() }],
        };
    }

    private sealed record ErrorEnvelope(ToolError Error);
}
