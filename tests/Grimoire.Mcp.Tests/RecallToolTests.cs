using Grimoire.Core;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

public sealed class RecallToolTests
{
    private static readonly float[] VectorA = [1, 0, 0];

    private static readonly float[] VectorB = [0, 1, 0];

    [Fact]
    public async Task Recall_BlankQuery_ReturnsInvalidRequest()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "  ",
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("query");
    }

    [Fact]
    public async Task Recall_NonPositiveLimit_ReturnsInvalidRequest()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "anything",
            ["limit"] = 0,
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("limit");
    }

    [Fact]
    public async Task Recall_ReturnsRankedHitsWithScoresAndBumpsAccessStats()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("We chose Wolverine over MassTransit", VectorA);
        session.Embeddings.Register("Unrelated grocery note", VectorB);
        session.Embeddings.Register("messaging stack decision", VectorA);
        await Remember(session, "We chose Wolverine over MassTransit", "grimoire");
        await Remember(session, "Unrelated grocery note", "grimoire");
        session.Clock.UtcNow = session.Clock.UtcNow.AddHours(1);

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "messaging stack decision",
            ["projectId"] = "grimoire",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("count").GetInt32().ShouldBe(2);
        var matches = payload.GetProperty("matches");
        matches.GetArrayLength().ShouldBe(2);
        var top = matches[0];
        top.GetProperty("content").GetString().ShouldBe("We chose Wolverine over MassTransit");
        top.GetProperty("similarity").GetDouble().ShouldBeGreaterThan(0.99);
        top.GetProperty("score").GetDouble().ShouldBeGreaterThan(0);
        top.GetProperty("memoryId").GetGuid().ShouldNotBe(Guid.Empty);
        top.GetProperty("actor").GetString().ShouldBe(McpTestSession.ClientActor);

        var record = session.Store.Points.Single(point => point.Record.Content == "We chose Wolverine over MassTransit").Record;
        record.AccessCount.ShouldBe(1);
        record.LastAccessedAt.ShouldBe(session.Clock.UtcNow);
    }

    [Fact]
    public async Task Recall_ByDefaultSearchesLongTermScopesOnly()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("project memory", VectorA);
        session.Embeddings.Register("session memory", VectorB);
        session.Embeddings.Register("project query", VectorA);
        await Remember(session, "project memory", "grimoire");
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "session memory",
            ["scope"] = "Session",
            ["sessionId"] = "s1",
        });

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "project query",
        });

        var matches = result.Payload().GetProperty("matches").EnumerateArray().ToArray();
        matches.ShouldContain(match => match.GetProperty("content").GetString() == "project memory");
        matches.ShouldNotContain(match => match.GetProperty("content").GetString() == "session memory");
    }

    [Fact]
    public async Task Recall_ScopeFilter_NarrowsToThatScope()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("shared text", VectorA);
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "shared text",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "shared text",
            ["scope"] = "Global",
        });
        session.Embeddings.Register("shared query", VectorA);

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "shared query",
            ["scope"] = "Global",
        });

        var matches = result.Payload().GetProperty("matches").EnumerateArray().ToArray();
        matches.ShouldNotBeEmpty();
        matches.ShouldAllBe(match => match.GetProperty("scope").GetString() == "Global");
    }

    [Fact]
    public async Task Recall_ExcludesTombstonedRecords()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("kept decision", VectorA);
        session.Embeddings.Register("retracted decision", VectorB);
        session.Embeddings.Register("any query", VectorA);
        await Remember(session, "kept decision", "grimoire");
        var retracted = await Remember(session, "retracted decision", "grimoire");
        var retractedId = retracted.Payload().GetProperty("memoryId").GetGuid();
        await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = retractedId.ToString(),
            ["reason"] = "human retracted it",
        });

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "any query",
        });

        var matches = result.Payload().GetProperty("matches").EnumerateArray().ToArray();
        matches.ShouldContain(match => match.GetProperty("content").GetString() == "kept decision");
        matches.ShouldNotContain(match => match.GetProperty("content").GetString() == "retracted decision");
    }

    [Fact]
    public async Task Recall_TomeParam_ReadsAnotherTomeAndEchoesIt()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("alpha squad decision", VectorA);
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "alpha squad decision",
            ["scope"] = "Global",
            ["kind"] = "Fact",
            ["tome"] = "alpha",
        });

        var own = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "alpha squad decision",
        });

        own.ShouldSucceed();
        own.Payload().GetProperty("count").GetInt32().ShouldBe(0);
        own.Payload().GetProperty("tome").GetString().ShouldBe(McpTestSession.ClientActor);

        var cross = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "alpha squad decision",
            ["tome"] = "alpha",
        });

        cross.ShouldSucceed();
        cross.Payload().GetProperty("count").GetInt32().ShouldBe(1);
        cross.Payload().GetProperty("tome").GetString().ShouldBe("alpha");
    }

    [Fact]
    public async Task Recall_InvalidTome_ReturnsInvalidRequest()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "anything",
            ["tome"] = "home/tome",
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("tome");
    }

    [Fact]
    public async Task Recall_TagsFilter_ReturnsARecordCarryingTheBoardLinkTag()
    {
        const string boardLinkTag = "overmind://entry/dbd64079-ea55-4b90-82a9-842aca5f3a28";
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("board decision provenance", VectorA);
        session.Embeddings.Register("unrelated grocery note", VectorB);
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "board decision provenance",
            ["scope"] = "Global",
            ["kind"] = "Fact",
            ["tags"] = new[] { boardLinkTag },
        });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "unrelated grocery note",
            ["scope"] = "Global",
        });

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "board decision provenance",
            ["tags"] = new[] { boardLinkTag },
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("count").GetInt32().ShouldBe(1);
        payload.GetProperty("matches")[0].GetProperty("tags").EnumerateArray()
            .Select(tag => tag.GetString()).ShouldBe([boardLinkTag]);
    }

    private static async Task<CallToolResult> Remember(McpTestSession session, string content, string projectId)
    {
        return await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = content,
            ["scope"] = "Project",
            ["projectId"] = projectId,
        });
    }
}
