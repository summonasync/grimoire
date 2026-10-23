using System.Text.Json;
using Grimoire.Core;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

public sealed class ScryToolTests
{
    [Fact]
    public async Task Scry_ExcludesTheOwnTomeByDefault_AndSearchesEveryOther()
    {
        await using var session = await McpTestSession.StartAsync();
        await RememberAsync(session, "own tome note", tome: null);
        await RememberAsync(session, "alpha squad decision", tome: "alpha");

        var result = await session.Client.CallToolAsync("memory_scry", new Dictionary<string, object?>
        {
            ["query"] = "alpha squad decision",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("tomes").EnumerateArray().Select(value => value.GetString()).ShouldBe(["alpha"]);
        payload.GetProperty("count").GetInt32().ShouldBe(1);
        payload.GetProperty("hits")[0].GetProperty("content").GetString().ShouldBe("alpha squad decision");
        payload.GetProperty("hits")[0].GetProperty("tome").GetString().ShouldBe("alpha");
    }

    [Fact]
    public async Task Scry_ByDefault_StripsActorFieldsEntirely()
    {
        await using var session = await McpTestSession.StartAsync(new McpHostOptions { Actor = new MemoryActor("bob", "qwen") });
        await RememberAsync(session, "qwen ships on fridays", tome: "alpha");

        var result = await session.Client.CallToolAsync("memory_scry", new Dictionary<string, object?>
        {
            ["query"] = "qwen ships on fridays",
        });

        result.ShouldSucceed();
        var hit = result.Payload().GetProperty("hits")[0];
        hit.TryGetProperty("actor", out _).ShouldBeFalse("scry must be anonymous by default");
    }

    [Fact]
    public async Task Scry_LightAttribution_KeepsOnlyTheAlias()
    {
        await using var session = await McpTestSession.StartAsync(new McpHostOptions { Actor = new MemoryActor("bob", "qwen") });
        await RememberAsync(session, "qwen ships on fridays", tome: "alpha");

        var result = await session.Client.CallToolAsync("memory_scry", new Dictionary<string, object?>
        {
            ["query"] = "qwen ships on fridays",
            ["attribution"] = "Light",
        });

        result.ShouldSucceed();
        var hit = result.Payload().GetProperty("hits")[0];
        hit.GetProperty("actor").GetString().ShouldBe("bob");
        hit.GetProperty("actor").GetString().ShouldNotContain("qwen");
    }

    [Fact]
    public async Task Scry_FullAttribution_KeepsAliasAndVendor()
    {
        await using var session = await McpTestSession.StartAsync(new McpHostOptions { Actor = new MemoryActor("bob", "qwen") });
        await RememberAsync(session, "qwen ships on fridays", tome: "alpha");

        var result = await session.Client.CallToolAsync("memory_scry", new Dictionary<string, object?>
        {
            ["query"] = "qwen ships on fridays",
            ["attribution"] = "Full",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("hits")[0].GetProperty("actor").GetString().ShouldBe("bob+qwen");
    }

    [Fact]
    public async Task Scry_Journal_IsOptIn()
    {
        await using var session = await McpTestSession.StartAsync();
        await RememberAsync(session, "wired the test rig before noon", tome: "alpha", kind: "Journal");

        var factsOnly = await session.Client.CallToolAsync("memory_scry", new Dictionary<string, object?>
        {
            ["query"] = "wired the test rig before noon",
        });

        factsOnly.ShouldSucceed();
        factsOnly.Payload().GetProperty("count").GetInt32().ShouldBe(0);

        var withJournal = await session.Client.CallToolAsync("memory_scry", new Dictionary<string, object?>
        {
            ["query"] = "wired the test rig before noon",
            ["includeJournal"] = true,
        });

        withJournal.ShouldSucceed();
        withJournal.Payload().GetProperty("count").GetInt32().ShouldBe(1);
        withJournal.Payload().GetProperty("hits")[0].GetProperty("kind").GetString().ShouldBe("Journal");
    }

    [Fact]
    public async Task Scry_SessionMemories_NeverCrossTomes()
    {
        await using var session = await McpTestSession.StartAsync();
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "session scratchpad note",
            ["scope"] = "Session",
            ["sessionId"] = "s1",
            ["tome"] = "alpha",
        });

        var result = await session.Client.CallToolAsync("memory_scry", new Dictionary<string, object?>
        {
            ["query"] = "session scratchpad note",
            ["includeJournal"] = true,
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("count").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Scry_TombstonedAndSuperseded_NeverSurface()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("rule", [1, 0, 0]);
        session.Embeddings.Register("rule v1", [1, 0, 0]);
        session.Embeddings.Register("rule v2", [1, 0.1f, 0]);
        session.Embeddings.Register("retracted alpha note", [0, 1, 0]);
        await RememberAsync(session, "rule v1", tome: "alpha");
        var retracted = await RememberAsync(session, "retracted alpha note", tome: "alpha");
        await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = retracted.GetProperty("memoryId").GetGuid().ToString(),
            ["reason"] = "retracted by the human",
            ["tome"] = "alpha",
        });
        await RememberAsync(session, "rule v2", tome: "alpha");

        var result = await session.Client.CallToolAsync("memory_scry", new Dictionary<string, object?>
        {
            ["query"] = "rule",
        });

        result.ShouldSucceed();
        var hits = result.Payload().GetProperty("hits");
        hits.GetArrayLength().ShouldBe(1);
        hits[0].GetProperty("content").GetString().ShouldBe("rule v2");
    }

    [Fact]
    public async Task Scry_ExplicitTomes_NarrowTheSearch()
    {
        await using var session = await McpTestSession.StartAsync();
        await RememberAsync(session, "alpha team decision", tome: "alpha");
        await RememberAsync(session, "beta team decision", tome: "beta");

        var result = await session.Client.CallToolAsync("memory_scry", new Dictionary<string, object?>
        {
            ["query"] = "team decision",
            ["tomes"] = new[] { "alpha" },
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("tomes").EnumerateArray().Select(value => value.GetString()).ShouldBe(["alpha"]);
        payload.GetProperty("hits")[0].GetProperty("content").GetString().ShouldBe("alpha team decision");
    }

    [Fact]
    public async Task Scry_WithoutOtherTomes_ReturnsAnHonestEmptyResult()
    {
        await using var session = await McpTestSession.StartAsync();
        await RememberAsync(session, "only own tome note", tome: null);

        var result = await session.Client.CallToolAsync("memory_scry", new Dictionary<string, object?>
        {
            ["query"] = "only own tome note",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("tomes").GetArrayLength().ShouldBe(0);
        payload.GetProperty("hits").GetArrayLength().ShouldBe(0);
        payload.GetProperty("count").GetInt32().ShouldBe(0);
        payload.GetProperty("attribution").GetString().ShouldBe("None");
    }

    [Fact]
    public async Task Scry_InvalidTomeInList_ReturnsInvalidRequest()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_scry", new Dictionary<string, object?>
        {
            ["query"] = "anything",
            ["tomes"] = new[] { "Not Valid!!" },
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("tomes");
    }

    [Fact]
    public async Task Scry_UnknownTomeKey_IsRejectedNamingTheKnownKeys()
    {
        await using var session = await McpTestSession.StartAsync();
        await RememberAsync(session, "alpha squad decision", tome: "alpha");

        var result = await session.Client.CallToolAsync("memory_scry", new Dictionary<string, object?>
        {
            ["query"] = "anything",
            ["tomes"] = new[] { "nonesuch-tome" },
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("tomes");
        message.ShouldContain("'nonesuch-tome'");
        message.ShouldContain("Known tome keys");
        message.ShouldContain("alpha");
        message.ShouldContain("byTome");
    }

    [Fact]
    public async Task Scry_RejectionKeys_MatchTheStatsByTomeKeys()
    {
        await using var session = await McpTestSession.StartAsync();
        await RememberAsync(session, "alpha squad decision", tome: "alpha");
        var retired = await RememberAsync(session, "beta retired note", tome: "beta");
        await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = retired.GetProperty("memoryId").GetGuid().ToString(),
            ["reason"] = "retire the beta note",
            ["tome"] = "beta",
        });

        var stats = await session.Client.CallToolAsync("memory_stats", new Dictionary<string, object?>());
        stats.ShouldSucceed();
        stats.Payload().GetProperty("byTome").EnumerateObject().Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal).ShouldBe(["alpha", "beta"]);

        var scry = await session.Client.CallToolAsync("memory_scry", new Dictionary<string, object?>
        {
            ["query"] = "anything",
            ["tomes"] = new[] { "gamma" },
        });

        var (_, message, _) = scry.Error();
        message.ShouldContain("alpha");
        message.ShouldContain("beta");
    }

    [Fact]
    public async Task Scry_MixedKnownAndUnknownTomes_RejectedWithoutPartialSearch()
    {
        await using var session = await McpTestSession.StartAsync();
        await RememberAsync(session, "alpha squad decision", tome: "alpha");

        var result = await session.Client.CallToolAsync("memory_scry", new Dictionary<string, object?>
        {
            ["query"] = "anything",
            ["tomes"] = new[] { "alpha", "nonesuch-tome" },
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("tomes");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Scry_BlankQuery_ReturnsInvalidRequest(string query)
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_scry", new Dictionary<string, object?>
        {
            ["query"] = query,
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("query");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Scry_NonPositiveLimit_ReturnsInvalidRequest(int limit)
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_scry", new Dictionary<string, object?>
        {
            ["query"] = "anything",
            ["limit"] = limit,
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("limit");
    }

    [Fact]
    public async Task Scry_IsRegisteredReadOnlyIdempotentAndNonDestructive()
    {
        await using var session = await McpTestSession.StartAsync();

        var tools = await session.Client.ListToolsAsync();
        var tool = tools.Single(item => item.Name == "memory_scry");

        var annotations = tool.ProtocolTool.Annotations.ShouldNotBeNull();
        annotations.ReadOnlyHint.ShouldBe(true);
        annotations.IdempotentHint.ShouldBe(true);
        annotations.DestructiveHint.ShouldBe(false);
    }

    private static async Task<JsonElement> RememberAsync(McpTestSession session, string content, string? tome, string kind = "Fact")
    {
        var arguments = new Dictionary<string, object?>
        {
            ["content"] = content,
            ["scope"] = "Global",
            ["kind"] = kind,
        };
        if (tome is not null)
        {
            arguments["tome"] = tome;
        }

        var result = await session.Client.CallToolAsync("memory_remember", arguments);
        result.ShouldSucceed();
        return result.Payload();
    }
}
