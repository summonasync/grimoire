using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

internal static class ResultJson
{
    public static JsonElement Payload(this CallToolResult result)
    {
        using var document = JsonDocument.Parse(result.Text());
        return document.RootElement.Clone();
    }

    public static string Text(this CallToolResult result)
    {
        return result.Content.OfType<TextContentBlock>().Single().Text;
    }

    public static void ShouldSucceed(this CallToolResult result)
    {
        (result.IsError ?? false).ShouldBeFalse($"unexpected tool error: {result.Text()}");
    }

    public static (string Code, string Message, string? Param) Error(this CallToolResult result)
    {
        (result.IsError ?? false).ShouldBeTrue("expected an isError=true tool result");
        var error = result.Payload().GetProperty("error");
        return (
            error.GetProperty("code").GetString()!,
            error.GetProperty("message").GetString()!,
            error.TryGetProperty("param", out var param) ? param.GetString() : null);
    }
}
