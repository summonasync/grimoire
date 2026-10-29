using Grimoire.Core;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

public sealed class HistoryToolTests
{
    private static readonly float[] VectorA = [1, 0, 0];

    private static readonly float[] VectorNearA = [1, 0.1f, 0];

    [Fact]
    public async Task History_MalformedMemoryId_ReturnsInvalidRequest()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_history", new Dictionary<string, object?>
        {
            ["memoryId"] = "not-a-guid",
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("memoryId");
    }

    [Fact]
    public async Task History_UnknownId_ReturnsFoundFalseWithEmptyChain()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_history", new Dictionary<string, object?>
        {
            ["memoryId"] = Guid.NewGuid().ToString(),
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("found").GetBoolean().ShouldBeFalse();
        payload.GetProperty("chain").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task History_SupersedeChain_WalksBothDirectionsOldestFirst()
    {
        await using var session = await McpTestSession.StartAsync();
        var firstId = await RememberAsync(session, "deploy on friday", VectorA);
        session.Clock.UtcNow = session.Clock.UtcNow.AddHours(1);
        var secondId = await RememberAsync(session, "deploy moved to monday", VectorNearA);
        session.Clock.UtcNow = session.Clock.UtcNow.AddHours(1);
        var thirdId = await RememberAsync(session, "deploy moved to tuesday", VectorNearA);

        var fromOriginal = await session.Client.CallToolAsync("memory_history", new Dictionary<string, object?>
        {
            ["memoryId"] = firstId.ToString(),
        });
        var fromCurrent = await session.Client.CallToolAsync("memory_history", new Dictionary<string, object?>
        {
            ["memoryId"] = thirdId.ToString(),
        });

        foreach (var result in new[] { fromOriginal, fromCurrent })
        {
            result.ShouldSucceed();
            var payload = result.Payload();
            payload.GetProperty("found").GetBoolean().ShouldBeTrue();
            payload.GetProperty("chain").EnumerateArray()
                .Select(entry => entry.GetProperty("memoryId").GetGuid())
                .ShouldBe(new[] { firstId, secondId, thirdId });
        }

        var chain = fromOriginal.Payload().GetProperty("chain").EnumerateArray().ToArray();
        chain.Select(entry => entry.GetProperty("status").GetString())
            .ShouldBe(["Superseded", "Superseded", "Active"]);
        chain.Select(entry => entry.GetProperty("bodyPreview").GetString())
            .ShouldBe(["deploy on friday", "deploy moved to monday", "deploy moved to tuesday"]);
        chain.Select(entry => entry.GetProperty("actor").GetString())
            .ShouldBe([McpTestSession.ClientName, McpTestSession.ClientName, McpTestSession.ClientName]);
        chain[0].TryGetProperty("tombstonedAt", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task History_TombstonedEntry_ShowsTombstoneMetadata()
    {
        await using var session = await McpTestSession.StartAsync();
        var firstId = await RememberAsync(session, "deploy on friday", VectorA);
        session.Clock.UtcNow = session.Clock.UtcNow.AddHours(1);
        var secondId = await RememberAsync(session, "deploy moved to monday", VectorNearA);
        await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = secondId.ToString(),
            ["reason"] = "retracted",
        });

        var result = await session.Client.CallToolAsync("memory_history", new Dictionary<string, object?>
        {
            ["memoryId"] = firstId.ToString(),
        });

        result.ShouldSucceed();
        var tombstoned = result.Payload().GetProperty("chain").EnumerateArray()
            .Single(entry => entry.GetProperty("memoryId").GetGuid() == secondId);
        tombstoned.GetProperty("status").GetString().ShouldBe(MemoryStatus.Tombstoned.ToString());
        tombstoned.GetProperty("tombstoneReason").GetString().ShouldBe("retracted");
        tombstoned.GetProperty("tombstonedAt").GetDateTimeOffset().ShouldBe(session.Clock.UtcNow);
    }

    [Fact]
    public async Task History_LongFirstLine_PreviewsAt120Characters()
    {
        await using var session = await McpTestSession.StartAsync();
        var memoryId = await RememberAsync(session, new string('x', 200), VectorA);

        var result = await session.Client.CallToolAsync("memory_history", new Dictionary<string, object?>
        {
            ["memoryId"] = memoryId.ToString(),
        });

        result.ShouldSucceed();
        var entry = result.Payload().GetProperty("chain").EnumerateArray().ShouldHaveSingleItem();
        entry.GetProperty("bodyPreview").GetString()!.Length.ShouldBe(120);
    }

    [Fact]
    public async Task History_MultiLineBody_PreviewsTheFirstLine()
    {
        await using var session = await McpTestSession.StartAsync();
        var memoryId = await RememberAsync(session, "deploy on friday\nwith rollback steps", VectorA);

        var result = await session.Client.CallToolAsync("memory_history", new Dictionary<string, object?>
        {
            ["memoryId"] = memoryId.ToString(),
        });

        result.ShouldSucceed();
        var entry = result.Payload().GetProperty("chain").EnumerateArray().ShouldHaveSingleItem();
        entry.GetProperty("bodyPreview").GetString().ShouldBe("deploy on friday");
    }

    [Fact]
    public async Task History_CrossTomeId_ReturnsFoundFalseAndEchoesTheTargetTome()
    {
        await using var session = await McpTestSession.StartAsync();
        var memoryId = await RememberAsync(session, "deploy on friday", VectorA, tome: "alpha");

        var result = await session.Client.CallToolAsync("memory_history", new Dictionary<string, object?>
        {
            ["memoryId"] = memoryId.ToString(),
            ["tome"] = "beta",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("found").GetBoolean().ShouldBeFalse();
        payload.GetProperty("tome").GetString().ShouldBe("beta");
        payload.GetProperty("chain").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task History_TomeParam_ReadsTheLineageInTheNamedTome()
    {
        await using var session = await McpTestSession.StartAsync();
        var memoryId = await RememberAsync(session, "deploy on friday", VectorA, tome: "alpha");

        var result = await session.Client.CallToolAsync("memory_history", new Dictionary<string, object?>
        {
            ["memoryId"] = memoryId.ToString(),
            ["tome"] = "alpha",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("found").GetBoolean().ShouldBeTrue();
        payload.GetProperty("tome").GetString().ShouldBe("alpha");
        payload.GetProperty("chain").EnumerateArray()
            .Select(entry => entry.GetProperty("memoryId").GetGuid())
            .ShouldBe([memoryId]);
    }

    [Fact]
    public async Task History_IsRegisteredReadOnlyIdempotentAndNonDestructive()
    {
        await using var session = await McpTestSession.StartAsync();

        var tools = await session.Client.ListToolsAsync();
        var tool = tools.Single(item => item.Name == "memory_history");

        var annotations = tool.ProtocolTool.Annotations.ShouldNotBeNull();
        annotations.ReadOnlyHint.ShouldBe(true);
        annotations.IdempotentHint.ShouldBe(true);
        annotations.DestructiveHint.ShouldBe(false);
    }

    private static async Task<Guid> RememberAsync(
        McpTestSession session,
        string content,
        float[] vector,
        string? tome = null)
    {
        session.Embeddings.Register(content, vector);
        var arguments = new Dictionary<string, object?>
        {
            ["content"] = content,
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
            ["kind"] = "Fact",
        };
        if (tome is not null)
        {
            arguments["tome"] = tome;
        }

        var remembered = await session.Client.CallToolAsync("memory_remember", arguments);
        remembered.ShouldSucceed();
        return remembered.Payload().GetProperty("memoryId").GetGuid();
    }


    [Fact]
    public async Task History_UnknownExplicitTome_CarriesTheNoneYetWarning()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_history", new Dictionary<string, object?>
        {
            ["memoryId"] = Guid.NewGuid().ToString(),
            ["tome"] = "nonesuch-tome",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("found").GetBoolean().ShouldBeFalse();
        payload.GetProperty("warnings")[0].GetString()
            .ShouldBe("tome 'nonesuch-tome' is empty or does not exist. Known tome keys: (none yet)");
    }

    [Fact]
    public async Task History_FoundLineageInExplicitTome_StaysSilent()
    {
        await using var session = await McpTestSession.StartAsync();
        var id = await RememberAsync(session, "alpha lineage fact", [1f, 0f, 0f], tome: "alpha");

        var result = await session.Client.CallToolAsync("memory_history", new Dictionary<string, object?>
        {
            ["memoryId"] = id.ToString(),
            ["tome"] = "alpha",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("found").GetBoolean().ShouldBeTrue();
        payload.TryGetProperty("warnings", out _).ShouldBeFalse();
    }
}
