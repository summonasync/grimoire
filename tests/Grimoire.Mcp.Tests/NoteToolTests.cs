using Grimoire.Core;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

public sealed class NoteToolTests
{
    [Fact]
    public async Task Note_ContentOnly_AutotagsProjectFromTheCallerPath()
    {
        await using var session = await McpTestSession.StartAsync(callerPath: "/src/grimoire");

        var result = await session.Client.CallToolAsync("memory_note", new Dictionary<string, object?>
        {
            ["content"] = "Ship gates stay green",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("scope").GetString().ShouldBe("Project");
        payload.GetProperty("projectId").GetString().ShouldBe("grimoire");
        payload.GetProperty("actor").GetString().ShouldBe(McpTestSession.ClientActor);
        payload.GetProperty("tome").GetString().ShouldBe(McpTestSession.ClientActor);
        payload.TryGetProperty("topicTag", out _).ShouldBeFalse("an unclassified note lands without a topic tag");

        var record = session.Store.Points.ShouldHaveSingleItem().Record;
        record.Kind.ShouldBe(MemoryKind.Journal);
        record.Importance.ShouldBe(0.5);
        record.Scope.ShouldBe(MemoryScope.Project);
        record.Tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task Note_UnresolvedCwd_RejectsNamingTheFix()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_note", new Dictionary<string, object?>
        {
            ["content"] = "We ship on Fridays",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.ProjectIdRequired);
        param.ShouldBe("projectId");
        message.ShouldContain(FakeServiceProvider.DefaultCallerPath);
        message.ShouldContain("alpha, beta, grimoire");
        message.ShouldContain("Nothing was applied by this call.");
        session.Store.Points.ShouldBeEmpty();
        session.Embeddings.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Note_UnknownProjectId_RejectsWithTheRegisteredSet()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_note", new Dictionary<string, object?>
        {
            ["content"] = "We ship on Fridays",
            ["projectId"] = "Slurpy.AI",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.ProjectIdUnknown);
        param.ShouldBe("projectId");
        message.ShouldContain("'Slurpy.AI' is not a registered project alias");
        message.ShouldContain("alpha, beta, grimoire");
        message.ShouldContain("Nothing was applied by this call.");
        session.Store.Points.ShouldBeEmpty();
        session.Embeddings.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Note_NeighborAgreement_StampsTheSharedTopicWithoutHint()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("deploy note one", [1f, 0.7f, 0]);
        session.Embeddings.Register("deploy note two", [1f, 0, 0.7f]);
        session.Embeddings.Register("deploy note three", [1f, 0, 0]);
        await Note(session, "deploy note one", topic: "deploy");
        await Note(session, "deploy note two", topic: "deploy");

        var result = await session.Client.CallToolAsync("memory_note", new Dictionary<string, object?>
        {
            ["content"] = "deploy note three",
            ["projectId"] = "grimoire",
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.GetProperty("topicTag").GetString().ShouldBe("topic:deploy");
        payload.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()!).ShouldBe(["topic:deploy"]);
        payload.GetProperty("nearDuplicates").EnumerateArray().Count().ShouldBe(2);
        session.Store.Points.ShouldContain(point => point.Record.Tags.Contains("topic:deploy"));
    }

    [Fact]
    public async Task Note_ExplicitTopic_OverridesTheNeighborVote()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("coffee note one", [1f, 0.7f, 0]);
        session.Embeddings.Register("coffee note two", [1f, 0, 0.7f]);
        session.Embeddings.Register("deploy note three", [1f, 0, 0]);
        await Note(session, "coffee note one", topic: "coffee");
        await Note(session, "coffee note two", topic: "coffee");

        var result = await session.Client.CallToolAsync("memory_note", new Dictionary<string, object?>
        {
            ["content"] = "deploy note three",
            ["projectId"] = "grimoire",
            ["topic"] = "Deploy Lane",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("topicTag").GetString().ShouldBe("topic:deploy-lane");
        session.Store.Points.Single(point => point.Record.Content == "deploy note three")
            .Record.Tags.ShouldBe(["topic:deploy-lane"]);
    }

    [Fact]
    public async Task Note_NoAgreement_LandsUntagged()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_note", new Dictionary<string, object?>
        {
            ["content"] = "Isolated observation",
            ["scope"] = "Global",
        });

        result.ShouldSucceed();
        result.Payload().TryGetProperty("topicTag", out _).ShouldBeFalse();
        result.Payload().TryGetProperty("nearDuplicates", out _).ShouldBeFalse();
        session.Store.Points.ShouldHaveSingleItem().Record.Tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task Note_IdenticalRetry_ReplaysIdempotently()
    {
        await using var session = await McpTestSession.StartAsync();
        var request = new Dictionary<string, object?>
        {
            ["content"] = "Ship gates stay green",
            ["projectId"] = "grimoire",
        };
        var first = await session.Client.CallToolAsync("memory_note", new Dictionary<string, object?>(request));

        var second = await session.Client.CallToolAsync("memory_note", request);

        var firstId = first.Payload().GetProperty("memoryId").GetGuid();
        var payload = second.Payload();
        payload.GetProperty("idempotentReplay").GetBoolean().ShouldBeTrue();
        payload.GetProperty("maintenancePending").GetBoolean().ShouldBeFalse();
        payload.GetProperty("memoryId").GetGuid().ShouldBe(firstId);
        session.Store.Points.ShouldHaveSingleItem();
        session.Embeddings.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Note_SessionScope_ExpiresWithinADay()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_note", new Dictionary<string, object?>
        {
            ["content"] = "Scratchpad thought",
            ["scope"] = "Session",
            ["sessionId"] = "s-1",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("scope").GetString().ShouldBe("Session");
        var record = session.Store.Points.ShouldHaveSingleItem().Record;
        record.ExpiresAt.ShouldNotBeNull();
        record.ExpiresAt!.Value.ShouldBe(record.CreatedAt.AddHours(24));
    }

    [Fact]
    public async Task Note_SessionWithProjectId_ReturnsParamConflict()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_note", new Dictionary<string, object?>
        {
            ["content"] = "Prefers dark mode",
            ["scope"] = "Session",
            ["sessionId"] = "s1",
            ["projectId"] = "grimoire",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.ParamConflict);
        param.ShouldBe("projectId");
        message.ShouldContain("Nothing was applied by this call.");
        session.Store.Points.ShouldBeEmpty();
    }

    [Fact]
    public async Task Note_SessionIdOutsideSessionScope_ReturnsParamConflict()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_note", new Dictionary<string, object?>
        {
            ["content"] = "Decision noted",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
            ["sessionId"] = "s1",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.ParamConflict);
        param.ShouldBe("sessionId");
        message.ShouldContain("Nothing was applied by this call.");
        session.Store.Points.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Note_BlankContent_RejectsWithoutTouchingTheStore(string content)
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_note", new Dictionary<string, object?>
        {
            ["content"] = content,
            ["scope"] = "Global",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("content");
        message.ShouldContain("Nothing was applied by this call.");
        session.Store.Points.ShouldBeEmpty();
        session.Embeddings.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Note_ExplicitTome_EchoesTheLandedTome()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_note", new Dictionary<string, object?>
        {
            ["content"] = "alpha squad observation",
            ["scope"] = "Global",
            ["tome"] = "Alpha Squad",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("tome").GetString().ShouldBe("alpha-squad");
        session.Store.Points.ShouldHaveSingleItem().Record.Tome.ShouldBe("alpha-squad");
    }

    [Fact]
    public async Task Note_InvalidTomeParam_RejectsWithoutTouchingTheStore()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_note", new Dictionary<string, object?>
        {
            ["content"] = "misbound note",
            ["scope"] = "Global",
            ["tome"] = "Not Valid!!",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("tome");
        message.ShouldContain("'Not Valid!!'");
        message.ShouldContain("Nothing was applied by this call.");
        session.Store.Points.ShouldBeEmpty();
        session.Embeddings.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Note_VotedTopic_ReportsWhoAgreedAndHowStrongly()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("theme note one", [1f, 0.7f, 0]);
        session.Embeddings.Register("theme note two", [1f, 0, 0.7f]);
        session.Embeddings.Register("theme note three", [1f, 0, 0]);
        await Note(session, "theme note one", topic: "onnx");
        await Note(session, "theme note two", topic: "onnx");

        var result = await session.Client.CallToolAsync("memory_note", new Dictionary<string, object?>
        {
            ["content"] = "theme note three",
            ["projectId"] = "grimoire",
        });

        var payload = result.Payload();
        payload.GetProperty("topicTag").GetString().ShouldBe("topic:onnx");
        payload.GetProperty("topicDecision").GetString().ShouldBe("Voted");
        var rationale = payload.GetProperty("topicRationale").GetString();
        rationale.ShouldContain("2 neighbors");
        rationale.ShouldContain("topic:onnx");
    }

    [Fact]
    public async Task Note_WeakConsensus_SaysWhyNothingWasStamped()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("fuzzy note one", [1f, 0.85f, 0]);
        session.Embeddings.Register("fuzzy note two", [1f, 0, 0.85f]);
        session.Embeddings.Register("fuzzy note three", [1f, 0, 0]);
        await Note(session, "fuzzy note one", topic: "onnx");
        await Note(session, "fuzzy note two", topic: "onnx");

        var result = await session.Client.CallToolAsync("memory_note", new Dictionary<string, object?>
        {
            ["content"] = "fuzzy note three",
            ["projectId"] = "grimoire",
        });

        var payload = result.Payload();
        payload.TryGetProperty("topicTag", out _).ShouldBeFalse("a below-confidence vote must not stamp");
        payload.GetProperty("topicDecision").GetString().ShouldBe("BelowConfidence");
        var rationale = payload.GetProperty("topicRationale").GetString();
        rationale.ShouldContain("topic:onnx");
        rationale.ShouldContain("nothing was stamped");
    }

    private static async Task Note(McpTestSession session, string content, string topic)
    {
        var result = await session.Client.CallToolAsync("memory_note", new Dictionary<string, object?>
        {
            ["content"] = content,
            ["projectId"] = "grimoire",
            ["topic"] = topic,
        });
        result.ShouldSucceed();
    }
}
