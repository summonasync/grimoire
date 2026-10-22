using Grimoire.Core;
using ModelContextProtocol.Client;

namespace Grimoire.Mcp.Tests;

public sealed class BriefToolTests
{
    [Fact]
    public async Task Brief_EmptyStore_ReturnsExplicitEmptyStates()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("persona").GetProperty("state").GetString().ShouldBe("Empty");
        payload.GetProperty("facts").GetProperty("state").GetString().ShouldBe("Empty");
        payload.GetProperty("tombstones").GetProperty("state").GetString().ShouldBe("Empty");
        payload.GetProperty("journal").GetProperty("state").GetString().ShouldBe("Empty");
        payload.GetProperty("facts").GetProperty("items").GetArrayLength().ShouldBe(0);
        payload.GetProperty("omitted").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Brief_EmptyTome_TeachesTheScryDoor()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        result.Payload().GetProperty("hint").GetString().ShouldContain("tome is empty — use memory_scry for cross-tome context");
    }

    [Fact]
    public async Task Brief_PopulatedTome_CarriesTheStartupContract()
    {
        await using var session = await McpTestSession.StartAsync();
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "standing rule",
            ["scope"] = "Global",
            ["kind"] = "Fact",
        });

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var hint = result.Payload().GetProperty("hint").GetString();
        hint.ShouldStartWith("startup contract");
        hint.ShouldNotContain("tome is empty");
    }

    [Fact]
    public async Task Brief_TomeParam_BriefsTheNamedTomeAndEchoesIt()
    {
        await using var session = await McpTestSession.StartAsync();
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "alpha squad standing rule",
            ["scope"] = "Global",
            ["kind"] = "Fact",
            ["tome"] = "alpha",
        });

        var alpha = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["tome"] = "alpha",
        });

        alpha.ShouldSucceed();
        var alphaPayload = alpha.Payload();
        alphaPayload.GetProperty("tome").GetString().ShouldBe("alpha");
        alphaPayload.GetProperty("facts").GetProperty("items")[0].GetProperty("content").GetString()
            .ShouldBe("alpha squad standing rule");

        var own = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        own.ShouldSucceed();
        own.Payload().GetProperty("facts").GetProperty("state").GetString().ShouldBe("Empty");
    }

    [Fact]
    public async Task Brief_InvalidTome_ReturnsInvalidRequest()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["tome"] = "tome!",
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("tome");
    }

    [Fact]
    public async Task Brief_PopulatedStore_ReturnsAllFourSections()
    {
        await using var session = await McpTestSession.StartAsync();
        await SeedPersonaAsync(session);
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "deploy gates on green tests",
            ["scope"] = "Project",
            ["kind"] = "Fact",
            ["projectId"] = "grimoire",
        });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "wired Serilog before the first run",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        });
        var retracted = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "roll our own vector math",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        });
        await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = retracted.Payload().GetProperty("memoryId").GetGuid().ToString(),
            ["reason"] = "log setup redone in the config PR",
        });

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["projectId"] = "grimoire",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("omitted").GetArrayLength().ShouldBe(0);

        var persona = payload.GetProperty("persona");
        persona.GetProperty("state").GetString().ShouldBe("Populated");
        persona.GetProperty("document").GetProperty("identity").GetString().ShouldBe("Senior .NET developer");

        var facts = payload.GetProperty("facts");
        facts.GetProperty("state").GetString().ShouldBe("Populated");
        facts.GetProperty("items")[0].GetProperty("content").GetString().ShouldBe("deploy gates on green tests");
        facts.GetProperty("items")[0].GetProperty("score").GetDouble().ShouldBeGreaterThan(0);

        var tombstones = payload.GetProperty("tombstones");
        tombstones.GetProperty("state").GetString().ShouldBe("Populated");
        var tombstone = tombstones.GetProperty("items")[0];
        tombstone.GetProperty("reason").GetString().ShouldBe("log setup redone in the config PR");
        tombstone.TryGetProperty("content", out _).ShouldBeFalse();

        var journal = payload.GetProperty("journal");
        journal.GetProperty("state").GetString().ShouldBe("Populated");
        journal.GetProperty("items")[0].GetProperty("content").GetString().ShouldBe("wired Serilog before the first run");
        journal.GetProperty("items")[0].GetProperty("truncated").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Brief_LongJournalEntry_IsCappedWithTruncatedMarker()
    {
        await using var session = await McpTestSession.StartAsync();
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = new string('x', 500),
            ["scope"] = "Global",
        });

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        var item = result.Payload().GetProperty("journal").GetProperty("items")[0];
        item.GetProperty("truncated").GetBoolean().ShouldBeTrue();
        item.GetProperty("content").GetString()!.Length.ShouldBe(400);
    }

    [Fact]
    public async Task Brief_LimitHints_AreClampedServerSide()
    {
        await using var session = await McpTestSession.StartAsync();
        foreach (var content in new[] { "one", "two", "three" })
        {
            await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
            {
                ["content"] = content,
                ["scope"] = "Global",
                ["kind"] = "Fact",
            });
            session.Clock.UtcNow = session.Clock.UtcNow.AddMinutes(1);
        }

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["factsPerScope"] = 0,
            ["journalPerScope"] = -5,
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("facts").GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Brief_ScopesOverride_NarrowsSections()
    {
        await using var session = await McpTestSession.StartAsync();
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "global fact",
            ["kind"] = "Fact",
            ["scope"] = "Global",
        });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "project fact",
            ["kind"] = "Fact",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        });

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>
        {
            ["scopes"] = new[] { "Global" },
        });

        var items = result.Payload().GetProperty("facts").GetProperty("items");
        items.GetArrayLength().ShouldBe(1);
        items[0].GetProperty("content").GetString().ShouldBe("global fact");
        items[0].GetProperty("scope").GetString().ShouldBe("Global");
    }

    [Fact]
    public async Task Brief_OverBudgetSections_AreNamedInOmitted()
    {
        var engineOptions = new MemoryEngineOptions { BriefMaxTotalChars = 5 };
        await using var session = await McpTestSession.StartAsync(engineOptions: engineOptions);
        await SeedPersonaAsync(session);
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "one small fact",
            ["scope"] = "Global",
            ["kind"] = "Fact",
        });
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "one journal note",
            ["scope"] = "Global",
        });
        var entry = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "retracted journal note",
            ["scope"] = "Global",
        });
        await session.Client.CallToolAsync("memory_forget", new Dictionary<string, object?>
        {
            ["memoryId"] = entry.Payload().GetProperty("memoryId").GetGuid().ToString(),
            ["reason"] = "stale note",
        });

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var payload = result.Payload();
        var omitted = payload.GetProperty("omitted").EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();
        omitted.ShouldBe(["persona", "facts", "tombstones", "journal"]);
        payload.GetProperty("persona").GetProperty("state").GetString().ShouldBe("Omitted");
        payload.GetProperty("facts").GetProperty("state").GetString().ShouldBe("Omitted");
        payload.GetProperty("facts").GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Brief_ScopesSchema_SpellsOutTheScopeValues()
    {
        await using var session = await McpTestSession.StartAsync();

        var schema = (await session.Client.ListToolMap())["memory_brief"].JsonSchema;
        var items = schema.GetProperty("properties").GetProperty("scopes").GetProperty("items");

        items.GetProperty("type").GetString().ShouldBe("string");
        items.GetProperty("enum").EnumerateArray()
            .Select(value => value.GetString())
            .ShouldBe(["Session", "Project", "Global", "Personal"]);
    }

    [Fact]
    public async Task Brief_IsRegisteredReadOnlyIdempotentAndNonDestructive()
    {
        await using var session = await McpTestSession.StartAsync();

        var tools = await session.Client.ListToolsAsync();
        var tool = tools.Single(item => item.Name == "memory_brief");

        var annotations = tool.ProtocolTool.Annotations.ShouldNotBeNull();
        annotations.ReadOnlyHint.ShouldBe(true);
        annotations.IdempotentHint.ShouldBe(true);
        annotations.DestructiveHint.ShouldBe(false);
    }

    private static async Task SeedPersonaAsync(McpTestSession session)
    {
        var persona = new PersonaService(session.Store, session.Embeddings, session.Clock);
        await persona.SetAsync(new PersonaDocument
        {
            Identity = "Senior .NET developer",
            Tone = "informal",
            Preferences = "dark mode, Vim bindings",
            Rules = "never commit without being asked",
        }, MemoryActor.Human, CancellationToken.None, McpTestSession.ClientActor);
    }
}
