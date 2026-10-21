using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

public sealed class SinglePayloadContractTests
{
    [Fact]
    public async Task EveryTool_RepliesWithOneJsonTextBlockAndNoStructuredContent()
    {
        await using var session = await McpTestSession.StartAsync();
        var results = await ToolSurfaceProbes.RunAsync(session);
        var registered = await session.Client.ListToolNames();

        results.Keys.OrderBy(name => name, StringComparer.Ordinal)
            .ShouldBe(registered.OrderBy(name => name, StringComparer.Ordinal), customMessage:
                "the probe fixture did not exercise every registered tool — the single-payload lock would silently skip one");

        foreach (var (tool, result) in results)
        {
            AssertSinglePayload(result, tool);
        }
    }

    [Fact]
    public async Task ErrorPath_KeepsTheSameSingleBlockRule()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "single payload contract error probe",
            ["scope"] = "Global",
            ["sessionId"] = "session-never-matches-global-scope",
        });

        (result.IsError ?? false).ShouldBeTrue(customMessage:
            "memory_remember probe is broken: scope 'Global' with sessionId must produce a ParamConflict error, " +
            "so the error-path lock below would be testing a success result");

        AssertSinglePayload(result, "memory_remember (error path)");
        result.Error().Code.ShouldBe("param_conflict", customMessage:
            "memory_remember (error path) must fail with the ParamConflict envelope, not some other error code");
    }

    private static void AssertSinglePayload(CallToolResult result, string tool)
    {
        result.StructuredContent.ShouldBeNull(customMessage:
            $"tool '{tool}' must not return StructuredContent — the JSON lives only in the single text block, " +
            "a second copy in StructuredContent is the duplicate payload this contract bans");

        var block = result.Content.ShouldHaveSingleItem(
            $"tool '{tool}' must reply with exactly one content block — a doubled response is the duplicate payload this contract bans")
            .ShouldBeAssignableTo<TextContentBlock>();

        try
        {
            using var document = JsonDocument.Parse(block.Text);
            document.RootElement.ValueKind.ShouldBe(JsonValueKind.Object, customMessage:
                $"tool '{tool}' single text block must hold a JSON object payload, " +
                $"got a {document.RootElement.ValueKind} root — the duplicate payload this contract bans");
        }
        catch (JsonException exception)
        {
            throw new ShouldAssertException(
                $"tool '{tool}' single text block must parse as a JSON object payload: {exception.Message}. Got: {Trim(block.Text)}");
        }
    }

    private static string Trim(string text) => text.Length > 200 ? text[..200] + "…" : text;
}
