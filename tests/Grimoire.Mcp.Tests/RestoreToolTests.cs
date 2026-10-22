using Grimoire.Core;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

public sealed class RestoreToolTests
{
    private static readonly float[] VectorA = [1, 0, 0];

    private static readonly float[] VectorNearA = [1, 0.1f, 0];

    [Fact]
    public async Task Restore_MalformedMemoryId_ReturnsInvalidRequest()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_restore", new Dictionary<string, object?>
        {
            ["memoryId"] = "not-a-guid",
            ["reason"] = "bring it back",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("memoryId");
        message.ShouldContain("'not-a-guid'");
        message.ShouldContain("Nothing was applied by this call.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Restore_BlankReason_ReturnsInvalidRequest(string reason)
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_restore", new Dictionary<string, object?>
        {
            ["memoryId"] = Guid.NewGuid().ToString(),
            ["reason"] = reason,
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("reason");
        message.ShouldContain($"reason: '{reason}'");
        message.ShouldContain("Nothing was applied by this call.");
    }

    [Fact]
    public async Task Restore_InvalidTome_EchoesValueAndStatesNothingApplied()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_restore", new Dictionary<string, object?>
        {
            ["memoryId"] = Guid.NewGuid().ToString(),
            ["reason"] = "bring it back",
            ["tome"] = "bad/tome",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("tome");
        message.ShouldContain("'bad/tome'");
        message.ShouldContain("Nothing was applied by this call.");
        session.Store.Points.ShouldBeEmpty();
    }

    [Fact]
    public async Task Restore_UnknownId_ReturnsRestoredFalseWithoutTombstoneEcho()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_restore", new Dictionary<string, object?>
        {
            ["memoryId"] = Guid.NewGuid().ToString(),
            ["reason"] = "bring it back",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("restored").GetBoolean().ShouldBeFalse();
        payload.TryGetProperty("reason", out _).ShouldBeFalse();
        payload.TryGetProperty("superseder", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Restore_TombstonedRecord_RevivesItAndEchoesTheReason()
    {
        await using var session = await McpTestSession.StartAsync();
        var memoryId = await RememberAsync(session, "deploy on friday", VectorA);
        await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = memoryId.ToString(),
            ["reason"] = "wrong day",
        });
        session.Clock.UtcNow = session.Clock.UtcNow.AddHours(1);

        var result = await session.Client.CallToolAsync("memory_restore", new Dictionary<string, object?>
        {
            ["memoryId"] = memoryId.ToString(),
            ["reason"] = "friday was right after all",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("restored").GetBoolean().ShouldBeTrue();
        payload.GetProperty("reason").GetString().ShouldBe("friday was right after all");

        var record = session.Store.Points.ShouldHaveSingleItem().Record;
        record.Status.ShouldBe(MemoryStatus.Active);
        record.TombstoneReason.ShouldBeNull();
        record.TombstonedAt.ShouldBeNull();
        record.Corrections.Count.ShouldBe(2);
        var trail = record.Corrections.Last();
        trail.Action.ShouldBe(CorrectionAction.Restore);
        trail.Reason.ShouldBe("friday was right after all");
        trail.Actor.Alias.ShouldBe(McpTestSession.ClientName);
    }

    [Fact]
    public async Task Restore_ActiveRecord_IsAnHonestNoOp()
    {
        await using var session = await McpTestSession.StartAsync();
        var memoryId = await RememberAsync(session, "deploy on friday", VectorA);

        var result = await session.Client.CallToolAsync("memory_restore", new Dictionary<string, object?>
        {
            ["memoryId"] = memoryId.ToString(),
            ["reason"] = "why not",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("restored").GetBoolean().ShouldBeFalse();
        session.Store.Points.Single().Record.Corrections.ShouldBeEmpty();
    }

    [Fact]
    public async Task Restore_SupersededRecord_ActivatesOriginalAndTombstonesTheSuperseder()
    {
        await using var session = await McpTestSession.StartAsync();
        var originalId = await RememberAsync(session, "deploy on friday", VectorA);
        session.Clock.UtcNow = session.Clock.UtcNow.AddHours(1);
        var successorId = await RememberAsync(session, "deploy moved to monday", VectorNearA);

        var result = await session.Client.CallToolAsync("memory_restore", new Dictionary<string, object?>
        {
            ["memoryId"] = originalId.ToString(),
            ["reason"] = "monday was a mistake",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("restored").GetBoolean().ShouldBeTrue();
        var superseder = payload.GetProperty("superseder");
        superseder.GetProperty("memoryId").GetGuid().ShouldBe(successorId);
        superseder.GetProperty("tombstoned").GetBoolean().ShouldBeTrue();
        superseder.GetProperty("tombstoneReason").GetString().ShouldBe($"restored {originalId}");

        session.Store.Points.Single(point => point.Record.Id == originalId).Record.Status
            .ShouldBe(MemoryStatus.Active);
        var successor = session.Store.Points.Single(point => point.Record.Id == successorId).Record;
        successor.Status.ShouldBe(MemoryStatus.Tombstoned);
        successor.TombstoneReason.ShouldBe($"restored {originalId}");
        session.Store.Points.Count(point => point.Record.Status == MemoryStatus.Active).ShouldBe(1);
    }

    [Fact]
    public async Task Restore_CrossTomeId_IsRefusedHonestlyAndEchoesTheTargetTome()
    {
        await using var session = await McpTestSession.StartAsync();
        var memoryId = await RememberAsync(session, "deploy on friday", VectorA, tome: "alpha");

        var result = await session.Client.CallToolAsync("memory_restore", new Dictionary<string, object?>
        {
            ["memoryId"] = memoryId.ToString(),
            ["reason"] = "cross tome attempt",
            ["tome"] = "beta",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("restored").GetBoolean().ShouldBeFalse();
        payload.GetProperty("tome").GetString().ShouldBe("beta");
        session.Store.Points.Single().Record.Status.ShouldBe(MemoryStatus.Active);
    }

    [Fact]
    public async Task Restore_TomeParam_RestoresWithinTheNamedTome()
    {
        await using var session = await McpTestSession.StartAsync();
        var memoryId = await RememberAsync(session, "deploy on friday", VectorA, tome: "alpha");
        await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = memoryId.ToString(),
            ["reason"] = "wrong day",
            ["tome"] = "alpha",
        });

        var result = await session.Client.CallToolAsync("memory_restore", new Dictionary<string, object?>
        {
            ["memoryId"] = memoryId.ToString(),
            ["reason"] = "friday was right",
            ["tome"] = "alpha",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("restored").GetBoolean().ShouldBeTrue();
        payload.GetProperty("tome").GetString().ShouldBe("alpha");
        session.Store.Points.Single().Record.Status.ShouldBe(MemoryStatus.Active);
    }

    [Fact]
    public async Task Restore_IsRegisteredNonReadOnlyNonDestructiveAndIdempotent()
    {
        await using var session = await McpTestSession.StartAsync();

        var tools = await session.Client.ListToolsAsync();
        var tool = tools.Single(item => item.Name == "memory_restore");

        var annotations = tool.ProtocolTool.Annotations.ShouldNotBeNull();
        annotations.ReadOnlyHint.ShouldBe(false);
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
}
