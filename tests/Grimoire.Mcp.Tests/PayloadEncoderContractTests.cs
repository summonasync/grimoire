using System.Text.RegularExpressions;

namespace Grimoire.Mcp.Tests;

public sealed class PayloadEncoderContractTests
{
    private const string CyrillicSample =
        "Решение оператора — уничтоженный тестами центральный стор не восстанавливать: «заново проиндексируем что надо»";

    private static readonly Regex BackslashUEscapes = new(@"\\u[0-9a-fA-F]{4}", RegexOptions.Compiled);

    [Fact]
    public async Task SuccessPath_CyrillicPayload_ReachesClientAsLiteralCharacters()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register(CyrillicSample, [1f, 0, 0]);

        var remembered = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = CyrillicSample,
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
            ["kind"] = "Fact",
        });
        remembered.ShouldSucceed();

        var listed = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["projectId"] = "grimoire",
        });
        listed.ShouldSucceed();

        var block = listed.Text();
        var payload = listed.Payload();

        payload.GetProperty("count").GetInt32().ShouldBe(1, customMessage:
            "the list probe must return exactly the one Cyrillic record — an empty payload would make the escape count below vacuous");
        payload.GetProperty("memories")[0].GetProperty("content").GetString().ShouldBe(CyrillicSample);

        CyrillicSample.Count(character => character > 127).ShouldBeGreaterThan(9, customMessage:
            "the sample must carry at least ten non-ASCII characters or this guard measures nothing");
        block.ShouldContain(CyrillicSample);
        block.Count(character => character > 127).ShouldBeGreaterThan(9);
        BackslashUEscapes.Matches(block).Count().ShouldBe(0, customMessage:
            "this text block is what survives exactly one JSON decode of the wire frame — backslash-u here is escaping " +
            "baked in by the response serializer (the JSON-RPC envelope is undone by that same decode and never shows up); " +
            "see ToolResults.ResponseJsonOptions");
    }

    [Fact]
    public async Task FailurePath_ErrorEnvelope_ReachesClientWithNoEscapeSequences()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "encoder guard error probe",
            ["scope"] = "Global",
            ["sessionId"] = "session-never-matches-global-scope",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe("param_conflict");
        param.ShouldBe("sessionId");
        message.ShouldContain("'session'");

        var block = result.Text();
        block.ShouldContain("scope 'session'");
        BackslashUEscapes.Matches(block).Count().ShouldBe(0, customMessage:
            "Failure must serialize through the same relaxed ResponseJsonOptions as Success — under the HTML-safe " +
            "default the message apostrophes rode the block as literal \\u0027 after one decode");
    }
}
