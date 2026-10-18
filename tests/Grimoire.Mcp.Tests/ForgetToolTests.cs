using Grimoire.Core;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

public sealed class ForgetToolTests
{
    [Fact]
    public async Task Forget_MalformedMemoryId_ReturnsInvalidRequest()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = "not-a-guid",
            ["reason"] = "cleanup",
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("memoryId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Forget_BlankReason_ReturnsInvalidRequest(string reason)
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = Guid.NewGuid().ToString(),
            ["reason"] = reason,
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("reason");
    }

    [Fact]
    public async Task Forget_UnknownId_ReturnsForgottenFalse()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = Guid.NewGuid().ToString(),
            ["reason"] = "cleanup",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("forgotten").GetBoolean().ShouldBeFalse();
        payload.TryGetProperty("reason", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Forget_ActiveRecord_TombstonesWithReasonWithoutDeleting()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("decision to retract", new float[] { 1, 0, 0 });
        var remembered = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "decision to retract",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        });
        var memoryId = remembered.Payload().GetProperty("memoryId").GetGuid();

        var result = await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = memoryId.ToString(),
            ["reason"] = "human retracted the decision",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("forgotten").GetBoolean().ShouldBeTrue();
        payload.GetProperty("reason").GetString().ShouldBe("human retracted the decision");

        var record = session.Store.Points.ShouldHaveSingleItem().Record;
        record.Status.ShouldBe(MemoryStatus.Tombstoned);
        record.TombstoneReason.ShouldBe("human retracted the decision");
        record.TombstonedAt.ShouldBe(session.Clock.UtcNow);
        session.Store.DeletedBatches.ShouldBeEmpty("forget never hard-deletes");
    }

    [Fact]
    public async Task Forget_AlreadyTombstoned_ReturnsForgottenFalse()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("decision to retract", new float[] { 1, 0, 0 });
        var remembered = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "decision to retract",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        });
        var memoryId = remembered.Payload().GetProperty("memoryId").GetGuid();
        await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = memoryId.ToString(),
            ["reason"] = "first retraction",
        });

        var result = await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = memoryId.ToString(),
            ["reason"] = "second retraction",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("forgotten").GetBoolean().ShouldBeFalse();
        session.Store.Points.Single().Record.TombstoneReason.ShouldBe("first retraction");
    }

    [Fact]
    public async Task Forget_CrossTomeId_IsRefusedHonestlyAndEchoesTheTargetTome()
    {
        await using var session = await McpTestSession.StartAsync();
        var remembered = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "decision to retract",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
            ["tome"] = "alpha",
        });
        var memoryId = remembered.Payload().GetProperty("memoryId").GetGuid();

        var result = await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = memoryId.ToString(),
            ["reason"] = "cross tome attempt",
            ["tome"] = "beta",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("forgotten").GetBoolean().ShouldBeFalse();
        payload.GetProperty("tome").GetString().ShouldBe("beta");
        session.Store.Points.Single().Record.Status.ShouldBe(MemoryStatus.Active);
    }

    [Fact]
    public async Task Forget_TomeParam_TombstonesWithinTheNamedTome()
    {
        await using var session = await McpTestSession.StartAsync();
        var remembered = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "decision to retract",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
            ["tome"] = "alpha",
        });
        var memoryId = remembered.Payload().GetProperty("memoryId").GetGuid();

        var result = await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = memoryId.ToString(),
            ["reason"] = "retracted in its own tome",
            ["tome"] = "alpha",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("forgotten").GetBoolean().ShouldBeTrue();
        payload.GetProperty("tome").GetString().ShouldBe("alpha");
        session.Store.Points.Single().Record.Status.ShouldBe(MemoryStatus.Tombstoned);
    }
}
