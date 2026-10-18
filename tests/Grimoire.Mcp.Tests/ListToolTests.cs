using Grimoire.Core;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

public sealed class ListToolTests
{
    [Fact]
    public async Task List_DefaultsToActiveRecordsOnly()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("kept", new float[] { 1, 0, 0 });
        session.Embeddings.Register("retracted", new float[] { 0, 1, 0 });
        var kept = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "kept",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        });
        var retracted = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "retracted",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        });
        await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = retracted.Payload().GetProperty("memoryId").GetGuid().ToString(),
            ["reason"] = "superseded by newer decision",
        });

        var active = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>());
        var tombstoned = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["status"] = "Tombstoned",
        });

        var activePayload = active.Payload();
        activePayload.GetProperty("count").GetInt32().ShouldBe(1);
        activePayload.GetProperty("memories")[0].GetProperty("memoryId").GetGuid()
            .ShouldBe(kept.Payload().GetProperty("memoryId").GetGuid());

        var tombstonedPayload = tombstoned.Payload();
        tombstonedPayload.GetProperty("count").GetInt32().ShouldBe(1);
        tombstonedPayload.GetProperty("memories")[0].GetProperty("tombstoneReason").GetString()
            .ShouldBe("superseded by newer decision");
    }

    [Fact]
    public async Task List_ReturnsNewestFirst()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("older", new float[] { 1, 0, 0 });
        session.Embeddings.Register("newer", new float[] { 0, 1, 0 });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "older",
            ["scope"] = "Global",
        });
        session.Clock.UtcNow = session.Clock.UtcNow.AddDays(1);
        var newer = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "newer",
            ["scope"] = "Global",
        });

        var result = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>());

        var memories = result.Payload().GetProperty("memories");
        memories.GetArrayLength().ShouldBe(2);
        memories[0].GetProperty("memoryId").GetGuid().ShouldBe(newer.Payload().GetProperty("memoryId").GetGuid());
    }

    [Fact]
    public async Task List_ProjectFilter_NarrowsToThatProject()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("alpha note", new float[] { 1, 0, 0 });
        session.Embeddings.Register("beta note", new float[] { 0, 1, 0 });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "alpha note",
            ["scope"] = "Project",
            ["projectId"] = "alpha",
        });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "beta note",
            ["scope"] = "Project",
            ["projectId"] = "beta",
        });

        var result = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["projectId"] = "alpha",
        });

        var memories = result.Payload().GetProperty("memories");
        memories.GetArrayLength().ShouldBe(1);
        memories[0].GetProperty("content").GetString().ShouldBe("alpha note");
    }

    [Fact]
    public async Task List_RespectsLimit()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("one", new float[] { 1, 0, 0 });
        session.Embeddings.Register("two", new float[] { 0, 1, 0 });
        session.Embeddings.Register("three", new float[] { 0, 0, 1 });
        foreach (var content in new[] { "one", "two", "three" })
        {
            await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
            {
                ["content"] = content,
                ["scope"] = "Global",
            });
            session.Clock.UtcNow = session.Clock.UtcNow.AddMinutes(1);
        }

        var result = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["limit"] = 2,
        });

        result.Payload().GetProperty("count").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task List_CarriesBodiesStatusAndBodyChars()
    {
        await using var session = await McpTestSession.StartAsync();
        const string content = "The deploy pipeline gates on green tests";
        session.Embeddings.Register(content, new float[] { 1, 0, 0 });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = content,
            ["scope"] = "Global",
            ["kind"] = "Fact",
        });

        var result = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>());

        var entry = result.Payload().GetProperty("memories")[0];
        entry.GetProperty("content").GetString().ShouldBe(content);
        entry.GetProperty("status").GetString().ShouldBe("Active");
        entry.GetProperty("kind").GetString().ShouldBe("Fact");
        entry.GetProperty("actor").GetString().ShouldBe(McpTestSession.ClientActor);
        entry.GetProperty("bodyChars").GetInt32().ShouldBe(content.Length);
    }

    [Fact]
    public async Task List_NonPositiveLimit_ReturnsInvalidRequest()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_list", new Dictionary<string, object?>
        {
            ["limit"] = -3,
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("limit");
    }
}
