using Grimoire.Core;
using ModelContextProtocol.Client;

namespace Grimoire.Mcp.Tests;

public sealed class TomeCreateToolTests
{
    [Fact]
    public async Task Create_FreshKey_ReturnsTheCreatedManifestWithActorProvenance()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_tome_create", new Dictionary<string, object?>
        {
            ["key"] = "probe-tome",
            ["description"] = "probe tomes live here",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("tome").GetString().ShouldBe("probe-tome");
        payload.GetProperty("description").GetString().ShouldBe("probe tomes live here");
        payload.GetProperty("createdBy").GetString().ShouldBe(McpTestSession.ClientActor);
        payload.GetProperty("createdAt").GetString().ShouldNotBeNull();
        payload.GetProperty("updatedAt").GetString().ShouldNotBeNull();

        var held = session.Manifests.Manifests.ShouldHaveSingleItem();
        held.Key.ShouldBe("probe-tome");
        held.CreatedBy.ShouldBe(McpTestSession.ClientActor);
    }

    [Fact]
    public async Task Create_NormalizesTheKeyBeforeStoring()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_tome_create", new Dictionary<string, object?>
        {
            ["key"] = "Probe Tome",
            ["description"] = "normalized on the way in",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("tome").GetString().ShouldBe("probe-tome");
        session.Manifests.Manifests.Single().Key.ShouldBe("probe-tome");
    }

    [Fact]
    public async Task Create_ExistingManifest_FailsWithTomeManifestExistsNamingTheTome()
    {
        await using var session = await McpTestSession.StartAsync();
        await session.Client.CallToolAsync("memory_tome_create", new Dictionary<string, object?>
        {
            ["key"] = "probe-tome",
            ["description"] = "first manifest",
        });

        var again = await session.Client.CallToolAsync("memory_tome_create", new Dictionary<string, object?>
        {
            ["key"] = "probe-tome",
            ["description"] = "second manifest",
        });

        var (code, message, param) = again.Error();
        code.ShouldBe(ToolErrorCodes.TomeManifestExists);
        param.ShouldBe("key");
        message.ShouldContain("probe-tome");
    }

    [Fact]
    public async Task Create_InvalidKey_FailsWithInvalidTomeNamingTheParam()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_tome_create", new Dictionary<string, object?>
        {
            ["key"] = "tome!",
            ["description"] = "never lands",
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidTome);
        param.ShouldBe("key");
        session.Manifests.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_BlankDescription_FailsWithInvalidDescriptionNamingTheConstraint()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_tome_create", new Dictionary<string, object?>
        {
            ["key"] = "probe-tome",
            ["description"] = "   ",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidDescription);
        param.ShouldBe("description");
        message.ShouldContain("1..280");
    }

    [Fact]
    public async Task Create_OverlengthDescription_FailsWithInvalidDescriptionNamingTheConstraint()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_tome_create", new Dictionary<string, object?>
        {
            ["key"] = "probe-tome",
            ["description"] = new string('x', 281),
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidDescription);
        param.ShouldBe("description");
        message.ShouldContain("1..280");
        message.ShouldContain("281");
    }

    [Fact]
    public async Task Create_TrimsSurroundingWhitespaceOffTheDescription()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_tome_create", new Dictionary<string, object?>
        {
            ["key"] = "probe-tome",
            ["description"] = "  padded description  ",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("description").GetString().ShouldBe("padded description");
    }

    [Fact]
    public async Task Create_IsRegisteredNotReadOnlyNotDestructiveAndNotIdempotent()
    {
        await using var session = await McpTestSession.StartAsync();

        var tools = await session.Client.ListToolsAsync();
        var tool = tools.Single(item => item.Name == "memory_tome_create");

        var annotations = tool.ProtocolTool.Annotations.ShouldNotBeNull();
        annotations.ReadOnlyHint.ShouldBe(false);
        annotations.DestructiveHint.ShouldBe(false);
        annotations.IdempotentHint.ShouldBe(false);
    }
}
