using System.Text.Json;
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

    [Fact]
    public async Task Recall_FromRegisteredCallerProject_DemotesForeignProjectHitsBelowHomeAndGlobal()
    {
        await using var session = await McpTestSession.StartAsync(callerPath: "/src/alpha");
        session.Embeddings.Register("alpha deployment fact", VectorA);
        session.Embeddings.Register("beta deployment fact", VectorA);
        session.Embeddings.Register("cross deployment fact", VectorA);
        session.Embeddings.Register("deployment question", VectorA);
        await Remember(session, "alpha deployment fact", "alpha");
        await Remember(session, "beta deployment fact", "beta");
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "cross deployment fact",
            ["scope"] = "Global",
        });

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "deployment question",
        });

        result.ShouldSucceed();
        var matches = result.Payload().GetProperty("matches").EnumerateArray().ToArray();
        matches.Length.ShouldBe(3);
        matches.Take(2).ShouldContain(match => ProjectIdOf(match) == "alpha");
        matches.Take(2).ShouldContain(match => ProjectIdOf(match) == null);
        matches[2].GetProperty("content").GetString().ShouldBe("beta deployment fact");
        matches[2].GetProperty("score").GetDouble().ShouldBeLessThan(matches[0].GetProperty("score").GetDouble());
    }

    [Fact]
    public async Task Recall_WithoutRegisteredCallerPath_NoDemotion()
    {
        await using var session = await McpTestSession.StartAsync(callerPath: "/agents/sandbox");
        session.Embeddings.Register("alpha deployment fact", VectorA);
        session.Embeddings.Register("beta deployment fact", VectorA);
        session.Embeddings.Register("deployment question", VectorA);
        await Remember(session, "alpha deployment fact", "alpha");
        await Remember(session, "beta deployment fact", "beta");

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "deployment question",
        });

        result.ShouldSucceed();
        var matches = result.Payload().GetProperty("matches").EnumerateArray().ToArray();
        matches.Length.ShouldBe(2);
        var scores = matches.Select(match => match.GetProperty("score").GetDouble()).ToArray();
        scores[0].ShouldBe(scores[1], 1e-9);
    }

    [Fact]
    public async Task Recall_Fields_KeepMemoryIdAndScore_AndOmitSimilarityUnlessRequested()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("We chose Wolverine over MassTransit", VectorA);
        session.Embeddings.Register("messaging stack decision", VectorA);
        await Remember(session, "We chose Wolverine over MassTransit", "grimoire");

        var projected = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "messaging stack decision",
            ["fields"] = new[] { "content" },
        });

        projected.ShouldSucceed();
        var hit = projected.Payload().GetProperty("matches")[0];
        KeysOf(hit).ShouldBe(["memoryId", "score", "content"], ignoreOrder: true);
        hit.GetProperty("content").GetString().ShouldBe("We chose Wolverine over MassTransit");
        hit.GetProperty("score").GetDouble().ShouldBeGreaterThan(0);
        hit.TryGetProperty("similarity", out _).ShouldBeFalse();

        var withSimilarity = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "messaging stack decision",
            ["fields"] = new[] { "content", "similarity" },
        });

        withSimilarity.ShouldSucceed();
        var similarityHit = withSimilarity.Payload().GetProperty("matches")[0];
        KeysOf(similarityHit).ShouldBe(["memoryId", "score", "content", "similarity"], ignoreOrder: true);
        similarityHit.GetProperty("similarity").GetDouble().ShouldBeGreaterThan(0.99);
    }

    [Fact]
    public async Task Recall_Compact_ReturnsTheDigestKeySetPlusScore()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("We chose Wolverine over MassTransit", VectorA);
        session.Embeddings.Register("messaging stack decision", VectorA);
        await Remember(session, "We chose Wolverine over MassTransit", "grimoire");

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "messaging stack decision",
            ["compact"] = true,
        });

        result.ShouldSucceed();
        var hit = result.Payload().GetProperty("matches")[0];
        KeysOf(hit).ShouldBe(["memoryId", "score", "content", "tags", "createdAt"], ignoreOrder: true);
    }

    [Fact]
    public async Task Recall_UnknownField_ReturnsInvalidRequestNamingTheAllowedSet()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "anything",
            ["fields"] = new[] { "bodyChars" },
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("fields");
        message.ShouldContain("bodyChars");
        message.ShouldContain("Allowed fields:");
        foreach (var field in new[]
                 {
                     "content", "scope", "kind", "actor", "tags", "projectId", "sessionId",
                     "createdAt", "lastAccessedAt", "similarity",
                 })
        {
            message.ShouldContain(field);
        }
    }

    [Fact]
    public async Task Recall_FieldsWithCompact_ReturnsInvalidRequest()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "anything",
            ["fields"] = new[] { "content" },
            ["compact"] = true,
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("fields");
        message.ShouldContain("mutually exclusive");
    }

    private static string[] KeysOf(JsonElement hit)
    {
        return [.. hit.EnumerateObject().Select(property => property.Name)];
    }

    private static string? ProjectIdOf(JsonElement hit)
    {
        return hit.TryGetProperty("projectId", out var project) && project.ValueKind == JsonValueKind.String
            ? project.GetString()
            : null;

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

    [Fact]
    public async Task Recall_SinceFilter_DropsMatchesOutsideTheWindow()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("old decision", VectorA);
        session.Embeddings.Register("new decision", VectorA);
        session.Embeddings.Register("decision", VectorA);
        await Remember(session, "old decision", "grimoire");
        session.Clock.UtcNow = session.Clock.UtcNow.AddDays(3);
        await Remember(session, "new decision", "grimoire");

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "decision",
            ["since"] = "2026-10-19",
        });

        result.ShouldSucceed();
        var matches = result.Payload().GetProperty("matches").EnumerateArray().ToArray();
        matches.Length.ShouldBe(1);
        matches[0].GetProperty("content").GetString().ShouldBe("new decision");
    }

    [Fact]
    public async Task Recall_UnparsableUntil_ReturnsInvalidRequest()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "anything",
            ["until"] = "eventually",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("until");
        message.ShouldContain("'until'");
        message.ShouldContain("eventually");
    }


    [Fact]
    public async Task Recall_UnknownTomeWithNoHits_CarriesTheKnownTomeWarning()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("alpha squad decision", VectorA);
        await Remember(session, "alpha squad decision", "grimoire");

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "anything",
            ["tome"] = "nonesuch-tome",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("count").GetInt32().ShouldBe(0);
        payload.GetProperty("warnings")[0].GetString()
            .ShouldBe("tome 'nonesuch-tome' is empty or does not exist. Known tome keys: test-client");
    }

    [Fact]
    public async Task Recall_KnownButEmptyTome_StaysSilent()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("journal scratch", VectorA);
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "journal scratch",
            ["scope"] = "Global",
            ["kind"] = "Journal",
            ["tome"] = "alpha",
        });

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "journal scratch",
            ["kind"] = "Fact",
            ["tome"] = "alpha",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("count").GetInt32().ShouldBe(0);
        payload.TryGetProperty("warnings", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Recall_WithHits_KeepsTheResponseWarningFree()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("alpha squad decision", VectorA);
        session.Embeddings.Register("alpha squad", VectorA);
        await Remember(session, "alpha squad decision", "grimoire");

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "alpha squad",
            ["tome"] = McpTestSession.ClientActor,
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("count").GetInt32().ShouldBe(1);
        result.Payload().TryGetProperty("warnings", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Recall_WithoutATomeParam_NeverWarns()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("anything", VectorA);

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "anything",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("count").GetInt32().ShouldBe(0);
        result.Payload().TryGetProperty("warnings", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Recall_WindowFillsWithRejects_WarnsNamingLimitAndRounds()
    {
        await using var session = await McpTestSession.StartAsync(storeDecorator: store => new WindowPaddingStore(store));
        session.Embeddings.Register("alpha squad decision", VectorA);
        await Remember(session, "alpha squad decision", "grimoire");

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "alpha squad decision",
            ["limit"] = 2,
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("count").GetInt32().ShouldBe(1);
        var warning = payload.GetProperty("warnings").EnumerateArray().Single().GetString();
        warning.ShouldNotBeNull();
        warning!.ShouldContain("after 4 rounds");
        warning.ShouldContain("limit 2");
        warning.ShouldContain("1 of limit 2 matches");
    }

    [Fact]
    public async Task Recall_StoreCameBackShort_SaysNothingAboutRounds()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("alpha squad decision", VectorA);
        await Remember(session, "alpha squad decision", "grimoire");

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "alpha squad decision",
            ["limit"] = 2,
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("count").GetInt32().ShouldBe(1);
        payload.TryGetProperty("warnings", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Recall_RoundCapOnATomeTheStoreDoesNotKnow_ShowsCapWarningAndTomeWarning()
    {
        await using var session = await McpTestSession.StartAsync(storeDecorator: store => new WindowPaddingStore(store));
        session.Embeddings.Register("anything", VectorA);

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "anything",
            ["tome"] = "nonesuch-tome",
            ["limit"] = 2,
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("count").GetInt32().ShouldBe(0);
        var warnings = payload.GetProperty("warnings").EnumerateArray().Select(value => value.GetString()!).ToArray();

        warnings.Length.ShouldBe(2);
        warnings[0].ShouldContain("after 4 rounds");
        warnings[1].ShouldStartWith("tome 'nonesuch-tome' is empty or does not exist");
        warnings.Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
    }

    [Fact]
    public async Task Recall_ProjectedHits_StillCarryTheRoundCapWarning()
    {
        await using var session = await McpTestSession.StartAsync(storeDecorator: store => new WindowPaddingStore(store));
        session.Embeddings.Register("alpha squad decision", VectorA);
        await Remember(session, "alpha squad decision", "grimoire");

        var result = await session.Client.CallToolAsync("memory_recall", new Dictionary<string, object?>
        {
            ["query"] = "alpha squad decision",
            ["limit"] = 2,
            ["compact"] = true,
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("warnings").EnumerateArray().ShouldHaveSingleItem();
    }
}
