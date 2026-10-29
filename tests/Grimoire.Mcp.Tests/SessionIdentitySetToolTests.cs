using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

public sealed class SessionIdentitySetToolTests
{
    [Fact]
    public async Task Set_EchoesTheStoredPairAndTheSessionScope()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("session_identity_set", new Dictionary<string, object?>
        {
            ["alias"] = "guldan",
            ["vendor"] = "claude",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("alias").GetString().ShouldBe("guldan");
        payload.GetProperty("vendor").GetString().ShouldBe("claude");
        payload.GetProperty("appliesTo").GetString().ShouldBe("this MCP session only");
    }

    [Fact]
    public async Task Set_TrimsBothValues_AndDropsABlankVendor()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("session_identity_set", new Dictionary<string, object?>
        {
            ["alias"] = "  guldan  ",
            ["vendor"] = "   ",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("alias").GetString().ShouldBe("guldan");
        payload.TryGetProperty("vendor", out _).ShouldBeFalse("a null vendor is omitted from the wire like every other view");
    }

    [Fact]
    public async Task Set_Again_LatestPairCarriesTheFollowingWrites()
    {
        await using var session = await McpTestSession.StartAsync();
        await session.Client.CallToolAsync("session_identity_set", new Dictionary<string, object?>
        {
            ["alias"] = "first-alias",
            ["vendor"] = "first-vendor",
        });

        var first = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "stamped by the first pair",
            ["scope"] = "Global",
        });
        await session.Client.CallToolAsync("session_identity_set", new Dictionary<string, object?>
        {
            ["alias"] = "second-alias",
        });
        var second = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "stamped by the second pair",
            ["scope"] = "Global",
        });

        first.ShouldSucceed();
        first.Payload().GetProperty("actor").GetString().ShouldBe("first-alias+first-vendor");
        second.ShouldSucceed();
        second.Payload().GetProperty("actor").GetString().ShouldBe("second-alias");
    }

    [Fact]
    public async Task Set_DoesNotTouchTomeResolution()
    {
        await using var session = await McpTestSession.StartAsync();
        await session.Client.CallToolAsync("session_identity_set", new Dictionary<string, object?>
        {
            ["alias"] = "tome-alias",
        });

        var result = await session.Client.CallToolAsync("memory_stats", new Dictionary<string, object?>());

        result.ShouldSucceed();
        result.Payload().GetProperty("tome").GetString().ShouldBe(McpTestSession.ClientActor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Set_ABlankAlias_ReturnsInvalidRequestNamingTheParameter(string alias)
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("session_identity_set", new Dictionary<string, object?>
        {
            ["alias"] = alias,
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("alias");
        message.ShouldContain("is required");
        message.ShouldContain("Nothing was applied by this call.");
    }

    [Fact]
    public async Task Set_ATooLongAlias_ReturnsInvalidRequestNamingTheParameter()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("session_identity_set", new Dictionary<string, object?>
        {
            ["alias"] = new string('a', 33),
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("alias");
        message.ShouldContain("the limit is 32");
        message.ShouldContain("Nothing was applied by this call.");
    }

    [Fact]
    public async Task Set_AnAliasWithInnerWhitespace_ReturnsInvalidRequestNamingTheParameter()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("session_identity_set", new Dictionary<string, object?>
        {
            ["alias"] = "gul dan",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("alias");
        message.ShouldContain("must not contain whitespace");
        message.ShouldContain("Nothing was applied by this call.");
    }

    [Fact]
    public async Task Set_ATooLongVendor_ReturnsInvalidRequestNamingTheParameter()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("session_identity_set", new Dictionary<string, object?>
        {
            ["alias"] = "guldan",
            ["vendor"] = new string('v', 33),
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("vendor");
        message.ShouldContain("the limit is 32");
        message.ShouldContain("Nothing was applied by this call.");
    }

    [Fact]
    public async Task Remember_WithARegisteredIdentityFile_StampsItsActor()
    {
        await using var session = await McpTestSession.StartAsync(
            identityFile: new FakeAgentIdentitySource(new MemoryActor("file-alias", "file-vendor")));

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "stamped by the identity file",
            ["scope"] = "Global",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("actor").GetString().ShouldBe("file-alias+file-vendor");
    }

    [Fact]
    public async Task Remember_TheSessionIdentity_OutsranksARegisteredIdentityFile()
    {
        await using var session = await McpTestSession.StartAsync(
            identityFile: new FakeAgentIdentitySource(new MemoryActor("file-alias", "file-vendor")));
        await session.Client.CallToolAsync("session_identity_set", new Dictionary<string, object?>
        {
            ["alias"] = "session-alias",
        });

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "stamped by the session identity",
            ["scope"] = "Global",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("actor").GetString().ShouldBe("session-alias");
    }

    [Fact]
    public async Task Remember_TheClaimAlias_OutsranksTheClientName()
    {
        await using var session = await McpTestSession.StartAsync(
            claims: new FakeActorClaimProvider(new ActorClaim { Alias = "claim-alias" }));

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "stamped by the claim alias",
            ["scope"] = "Global",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("actor").GetString().ShouldBe("claim-alias");
    }
}
