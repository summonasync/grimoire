using Grimoire.Core;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

public sealed class RememberToolTests
{
    private static readonly float[] VectorA = [1, 0, 0];

    private static readonly float[] VectorB = [0, 1, 0];

    private static readonly float[] VectorNearA = [1, 0.1f, 0];

    [Fact]
    public async Task Remember_ProjectScopeWithoutProjectId_ReturnsStructuredError()
    {
        await using var session = await McpTestSession.StartAsync(new McpHostOptions { Actor = new MemoryActor("bob", "qwen") });

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "We ship on Fridays",
            ["scope"] = "Project",
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.ProjectIdRequired);
        param.ShouldBe("projectId");
        session.Store.Points.ShouldBeEmpty();
        session.Embeddings.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Global")]
    [InlineData("Session")]
    [InlineData("Personal")]
    public async Task Remember_ProjectIdOutsideProjectScope_ReturnsParamConflict(string scope)
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "Prefers dark mode",
            ["scope"] = scope,
            ["projectId"] = "grimoire",
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.ParamConflict);
        param.ShouldBe("projectId");
    }

    [Fact]
    public async Task Remember_SessionIdOutsideSessionScope_ReturnsParamConflict()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "Decision recorded",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
            ["sessionId"] = "s1",
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.ParamConflict);
        param.ShouldBe("sessionId");
    }

    [Fact]
    public async Task Remember_FactInSessionScope_ReturnsInvalidRequest()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "Durable fact",
            ["scope"] = "Session",
            ["sessionId"] = "s1",
            ["kind"] = "Fact",
        });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("kind");
        message.ShouldContain("journal");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Remember_BlankContent_ReturnsInvalidRequest(string content)
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = content,
            ["scope"] = "Global",
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("content");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-0.5)]
    public async Task Remember_ImportanceOutOfRange_ReturnsInvalidRequest(double importance)
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "Weighted note",
            ["scope"] = "Global",
            ["importance"] = importance,
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("importance");
    }

    [Fact]
    public async Task Remember_ValidWrite_EchoesCompactReferenceWithoutBody()
    {
        await using var session = await McpTestSession.StartAsync(new McpHostOptions { Actor = new MemoryActor("bob", "qwen") });
        session.Embeddings.Register("Uses Mediator over MediatR", VectorA);

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "Uses Mediator over MediatR",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
            ["tags"] = new[] { "stack", "messaging" },
            ["importance"] = 0.9,
        });

        result.ShouldSucceed();
        var payload = result.Payload();
        payload.TryGetProperty("content", out _).ShouldBeFalse("mutation echoes must never carry the stored body");
        payload.GetProperty("memoryId").GetGuid().ShouldNotBe(Guid.Empty);
        payload.GetProperty("scope").GetString().ShouldBe("Project");
        payload.GetProperty("kind").GetString().ShouldBe("Journal");
        payload.GetProperty("actor").GetString().ShouldBe("bob+qwen");
        payload.GetProperty("projectId").GetString().ShouldBe("grimoire");
        payload.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()!).ShouldBe(["stack", "messaging"]);
        payload.GetProperty("supersededExisting").GetBoolean().ShouldBeFalse();
        payload.GetProperty("bodyChars").GetInt32().ShouldBe("Uses Mediator over MediatR".Length);

        var record = session.Store.Points.ShouldHaveSingleItem().Record;
        record.Content.ShouldBe("Uses Mediator over MediatR");
        record.Actor.Alias.ShouldBe("bob");
        record.Actor.Vendor.ShouldBe("qwen");
        record.Importance.ShouldBe(0.9);
    }

    [Fact]
    public async Task Remember_WithoutConfiguredActor_FallsBackToTheClientName()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "Rides the morning bus",
            ["scope"] = "Personal",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("actor").GetString().ShouldBe(McpTestSession.ClientName);
    }

    [Fact]
    public async Task Remember_NearDuplicate_SupersedesAndEchoesTheReplacedId()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("decision v1", VectorA);
        session.Embeddings.Register("decision v2", VectorNearA);

        var first = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "decision v1",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        });
        var second = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "decision v2",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        });

        var firstId = first.Payload().GetProperty("memoryId").GetGuid();
        second.Payload().GetProperty("supersededExisting").GetBoolean().ShouldBeTrue();
        second.Payload().GetProperty("supersededMemoryId").GetGuid().ShouldBe(firstId);
        session.Store.Points.Single(point => point.Record.Id == firstId).Record.Status
            .ShouldBe(MemoryStatus.Superseded);
    }

    [Fact]
    public async Task Remember_SameTextInAnotherProject_DoesNotSupersede()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("decision v1", VectorA);
        session.Embeddings.Register("decision v2", VectorNearA);

        var first = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "decision v1",
            ["scope"] = "Project",
            ["projectId"] = "alpha",
        });
        var second = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "decision v2",
            ["scope"] = "Project",
            ["projectId"] = "beta",
        });

        first.Payload().GetProperty("memoryId").GetGuid().ShouldNotBe(Guid.Empty);
        second.Payload().GetProperty("supersededExisting").GetBoolean().ShouldBeFalse();
        var points = session.Store.Points;
        points.ShouldContain(point => point.Record.ProjectId == "alpha" && point.Record.Status == MemoryStatus.Active);
        points.ShouldContain(point => point.Record.ProjectId == "beta" && point.Record.Status == MemoryStatus.Active);
    }

    [Fact]
    public async Task Remember_WhenEmbeddingTimesOut_ReturnsUpstreamUnavailable()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Failure = new TaskCanceledException("The request was canceled due to HttpClient.Timeout");

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "Worth remembering",
            ["scope"] = "Global",
        });

        var (code, message, _) = result.Error();
        code.ShouldBe(ToolErrorCodes.UpstreamUnavailable);
        message.ShouldNotContain("Exception");
        result.Text().ShouldContain("upstream_unavailable");
        session.Store.Points.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remember_TypedUpstreamFailure_MapsByTypeWithoutRawDiagnostics()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Failure = new UpstreamUnavailableException("ollama", new HttpRequestException("connection refused"));

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "Worth remembering",
            ["scope"] = "Global",
        });

        var (code, message, _) = result.Error();
        code.ShouldBe(ToolErrorCodes.UpstreamUnavailable);
        message.ShouldContain("ollama");
        message.ShouldContain("Nothing was written");
        message.ShouldNotContain("connection refused");
        session.Store.Points.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remember_IdenticalRetry_ReplaysOriginalWithoutNewWrite()
    {
        await using var session = await McpTestSession.StartAsync();

        var request = new Dictionary<string, object?>
        {
            ["content"] = "Ship on Fridays",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        };
        var first = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>(request));
        var second = await session.Client.CallToolAsync("memory_remember", request);

        var firstId = first.Payload().GetProperty("memoryId").GetGuid();
        var payload = second.Payload();
        payload.GetProperty("idempotentReplay").GetBoolean().ShouldBeTrue();
        payload.GetProperty("maintenancePending").GetBoolean().ShouldBeFalse();
        payload.GetProperty("memoryId").GetGuid().ShouldBe(firstId);
        session.Store.Points.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Remember_CommittedWithFailedMaintenance_ReportsPendingWithoutFailing()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Register("decision v1", VectorA);
        session.Embeddings.Register("decision v2", VectorNearA);

        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "decision v1",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        });
        session.Store.UpdateFailure = new InvalidOperationException("supersede update failed");

        var second = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "decision v2",
            ["scope"] = "Project",
            ["projectId"] = "grimoire",
        });

        second.ShouldSucceed();
        var payload = second.Payload();
        payload.GetProperty("maintenancePending").GetBoolean().ShouldBeTrue();
        payload.GetProperty("idempotentReplay").GetBoolean().ShouldBeFalse();
        session.Store.Points.Count(point => point.Record.Status == MemoryStatus.Active).ShouldBe(2);
    }

    [Fact]
    public async Task Remember_WhenEmbeddingFailsUnexpectedly_ReturnsInternalError()
    {
        await using var session = await McpTestSession.StartAsync();
        session.Embeddings.Failure = new InvalidOperationException("Ollama returned garbage");

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "Worth remembering",
            ["scope"] = "Global",
        });

        var (code, message, _) = result.Error();
        code.ShouldBe(ToolErrorCodes.InternalError);
        message.ShouldContain("Ollama returned garbage");
        message.ShouldNotContain("at Grimoire");
        session.Store.Points.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remember_ExplicitTome_WritesThereAndEchoesTheLandedTome()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "alpha squad rule",
            ["scope"] = "Global",
            ["kind"] = "Fact",
            ["tome"] = "Alpha Squad",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("tome").GetString().ShouldBe("alpha-squad");
        session.Store.Points.Single().Record.Tome.ShouldBe("alpha-squad");
    }

    [Fact]
    public async Task Remember_WithoutTome_BindsTheClientFallbackTome()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "client scoped note",
            ["scope"] = "Global",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("tome").GetString().ShouldBe(McpTestSession.ClientActor);
    }

    [Fact]
    public async Task Remember_EnvTome_BindsTheWriteWhenNoParamIsGiven()
    {
        await using var session = await McpTestSession.StartAsync(new McpHostOptions { EnvTome = "team-platform" });

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "team scoped note",
            ["scope"] = "Global",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("tome").GetString().ShouldBe("team-platform");
    }

    [Fact]
    public async Task Remember_EnvActorAlias_ImpliesTheTome()
    {
        await using var session = await McpTestSession.StartAsync(new McpHostOptions { EnvActor = "dan+claude" });

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "actor scoped note",
            ["scope"] = "Global",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("tome").GetString().ShouldBe("dan");
    }

    [Fact]
    public async Task Remember_ExplicitTome_BeatsTheEnvCandidates()
    {
        await using var session = await McpTestSession.StartAsync(new McpHostOptions
        {
            EnvTome = "team-platform",
            EnvActor = "dan+claude",
        });

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "pinned note",
            ["scope"] = "Global",
            ["tome"] = "alpha",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("tome").GetString().ShouldBe("alpha");
    }

    [Fact]
    public async Task Remember_ClaimBlock_SuppliesTheTomeAfterTheEnv()
    {
        var claims = new FakeActorClaimProvider(new ActorClaim { Alias = "kadre", Tome = "kadr-tome" });
        await using var session = await McpTestSession.StartAsync(claims: claims);

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "claimed note",
            ["scope"] = "Global",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("tome").GetString().ShouldBe("kadr-tome");
    }

    [Fact]
    public async Task Remember_ClaimAliasOnly_ImpliesTheTome()
    {
        var claims = new FakeActorClaimProvider(new ActorClaim { Alias = "kadre" });
        await using var session = await McpTestSession.StartAsync(claims: claims);

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "claimed alias note",
            ["scope"] = "Global",
        });

        result.ShouldSucceed();
        result.Payload().GetProperty("tome").GetString().ShouldBe("kadre");
    }

    [Fact]
    public async Task Remember_InvalidTomeParam_ReturnsInvalidRequestWithoutTouchingTheStore()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "misbound note",
            ["scope"] = "Global",
            ["tome"] = "Not Valid!!",
        });

        var (code, _, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("tome");
        session.Store.Points.ShouldBeEmpty();
        session.Embeddings.Requests.ShouldBeEmpty();
    }
}
