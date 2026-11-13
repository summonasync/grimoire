using System.Net;
using System.Threading.Channels;
using Grimoire.Core;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

public sealed class SessionCaptureTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task FirstRequest_WritesOneRowWithTheObservedHandshakeFields()
    {
        var faults = new RecordingSessionCaptureFaultSink();
        await using var session = await StartAsync(faults);

        await session.Client.ListToolsAsync().AsTask().WaitAsync(Wait);
        var row = await session.Sessions.AttemptedAsync.WaitAsync(Wait);

        row.ClientName.ShouldBe(McpTestSession.ClientName);
        row.ClientVersion.ShouldBe("0.1.0");
        row.ProtocolVersion.ShouldBe(session.Client.NegotiatedProtocolVersion);
        row.ActorAlias.ShouldBe(McpTestSession.ClientName);
        row.ActorVendor.ShouldBeNull();
        row.Transport.ShouldBe("stdio");
        row.Host.ShouldBe(Dns.GetHostName());
        row.OccurredAt.ShouldBe(session.Clock.UtcNow);
        row.SchemaVersion.ShouldBe(SessionRegistryEntry.CurrentSchemaVersion);

        await session.Client.ListToolsAsync().AsTask().WaitAsync(Wait);
        await session.Client.CallToolAsync("memory_stats", EmptyArgs()).AsTask().WaitAsync(Wait);
        await Task.Delay(200);

        session.Sessions.Attempts.ShouldBe(1);
        faults.Lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task FirstRequest_OnTheInitializeRevision_WritesOneRow()
    {
        var faults = new RecordingSessionCaptureFaultSink();
        await using var session = await StartAsync(faults, clientProtocolVersion: "2025-06-18");

        await session.Client.ListToolsAsync().AsTask().WaitAsync(Wait);
        var row = await session.Sessions.AttemptedAsync.WaitAsync(Wait);

        session.Client.NegotiatedProtocolVersion.ShouldBe("2025-06-18");
        row.ProtocolVersion.ShouldBe("2025-06-18");
        row.ClientName.ShouldBe(McpTestSession.ClientName);

        await session.Client.CallToolAsync("memory_stats", EmptyArgs()).AsTask().WaitAsync(Wait);
        await Task.Delay(200);

        session.Sessions.Attempts.ShouldBe(1);
        faults.Lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task FirstRequest_OnTheInitializeRevision_CapturesFullClientIdentityAtWriteTime()
    {
        var faults = new RecordingSessionCaptureFaultSink();
        await using var session = await StartAsync(faults, clientProtocolVersion: "2025-06-18");

        await session.Client.ListToolsAsync().AsTask().WaitAsync(Wait);
        var row = await session.Sessions.AttemptedAsync.WaitAsync(Wait);

        row.ClientName.ShouldNotBeNull();
        row.ClientVersion.ShouldNotBeNull();
        row.ProtocolVersion.ShouldNotBeNull();
        row.ProtocolVersion.ShouldBe("2025-06-18");
        faults.Lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task SessionIdentitySet_LaterInTheConnection_WritesNoSecondRow()
    {
        await using var session = await StartAsync(new RecordingSessionCaptureFaultSink());

        await session.Client.ListToolsAsync().AsTask().WaitAsync(Wait);
        await session.Sessions.AttemptedAsync.WaitAsync(Wait);

        await session.Client.CallToolAsync("session_identity_set", new Dictionary<string, object?>
        {
            ["alias"] = "guldan",
            ["vendor"] = "qwen",
        }).AsTask().WaitAsync(Wait);
        await session.Client.CallToolAsync("session_identity_set", new Dictionary<string, object?>
        {
            ["alias"] = "guldan",
            ["vendor"] = "claude",
        }).AsTask().WaitAsync(Wait);
        await Task.Delay(200);

        session.Sessions.Attempts.ShouldBe(1);
        session.Sessions.Written[0].ActorAlias.ShouldBe(McpTestSession.ClientName);
        session.Sessions.Written[0].ActorVendor.ShouldBeNull();
    }

    [Fact]
    public async Task SessionIdentitySet_PatchesTheCapturedRow_Once()
    {
        var faults = new RecordingSessionCaptureFaultSink();
        await using var session = await StartAsync(faults);

        await session.Client.ListToolsAsync().AsTask().WaitAsync(Wait);
        await session.Sessions.AttemptedAsync.WaitAsync(Wait);

        await session.Client.CallToolAsync("session_identity_set", IdentityArgs("tucker", "qwen"))
            .AsTask().WaitAsync(Wait);
        var refinement = await session.Sessions.FirstRefinementAsync.WaitAsync(Wait);

        refinement.Id.ShouldBe(session.Sessions.StoredIds.Single());
        refinement.Alias.ShouldBe("tucker");
        refinement.Vendor.ShouldBe("qwen");

        await session.Client.CallToolAsync("session_identity_set", IdentityArgs("second", "claude"))
            .AsTask().WaitAsync(Wait);
        await Task.Delay(200);

        session.Sessions.Refinements.Count.ShouldBe(1);
        session.Sessions.Attempts.ShouldBe(1);
        faults.Lines.ShouldBeEmpty();
    }

    [Fact]
    public async Task SessionIdentitySet_WithoutVendor_PatchesTheAliasAndPassesNoVendor()
    {
        await using var session = await StartAsync(new RecordingSessionCaptureFaultSink());

        await session.Client.ListToolsAsync().AsTask().WaitAsync(Wait);
        await session.Sessions.AttemptedAsync.WaitAsync(Wait);

        await session.Client.CallToolAsync("session_identity_set", IdentityArgs("solo", null))
            .AsTask().WaitAsync(Wait);
        var refinement = await session.Sessions.FirstRefinementAsync.WaitAsync(Wait);

        refinement.Alias.ShouldBe("solo");
        refinement.Vendor.ShouldBeNull();
    }

    [Fact]
    public async Task SessionIdentitySet_RejectedByValidation_LeavesTheRowUntouched()
    {
        await using var session = await StartAsync(new RecordingSessionCaptureFaultSink());

        await session.Client.ListToolsAsync().AsTask().WaitAsync(Wait);
        await session.Sessions.AttemptedAsync.WaitAsync(Wait);

        await session.Client.CallToolAsync("session_identity_set", IdentityArgs("two words", "qwen"))
            .AsTask().WaitAsync(Wait);
        await session.Client.CallToolAsync("session_identity_set", IdentityArgs("tucker", "qwen"))
            .AsTask().WaitAsync(Wait);
        var refinement = await session.Sessions.FirstRefinementAsync.WaitAsync(Wait);

        refinement.Alias.ShouldBe("tucker");
        session.Sessions.Refinements.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RefinementOutage_ServesTheIdentityCall_AndFaultsOnce()
    {
        var sessions = new FakeSessionRegistry { RefineFailure = new UpstreamUnavailableException("qdrant") };
        var faults = new RecordingSessionCaptureFaultSink();
        await using var session = await StartAsync(faults, sessions: sessions);

        await session.Client.ListToolsAsync().AsTask().WaitAsync(Wait);
        await session.Sessions.AttemptedAsync.WaitAsync(Wait);

        var result = await session.Client.CallToolAsync("session_identity_set", IdentityArgs("tucker", "qwen"))
            .AsTask().WaitAsync(Wait);
        var line = await faults.FirstLineAsync.WaitAsync(Wait);

        result.ShouldSucceed();
        line.ShouldContain("refinement");

        await session.Client.CallToolAsync("session_identity_set", IdentityArgs("second", "claude"))
            .AsTask().WaitAsync(Wait);
        await Task.Delay(300);

        sessions.Refinements.Count.ShouldBe(1);
        sessions.Attempts.ShouldBe(1);
        faults.Lines.Count.ShouldBe(1);
    }

    [Fact]
    public async Task StoreOutage_ServesTheRequest_LogsOneFaultLine_AndRetriesNothing()
    {
        var sessions = new FakeSessionRegistry { Failure = new UpstreamUnavailableException("qdrant") };
        var faults = new RecordingSessionCaptureFaultSink();
        await using var session = await StartAsync(faults, sessions: sessions);

        var result = await session.Client.CallToolAsync("memory_stats", EmptyArgs()).AsTask().WaitAsync(Wait);
        var line = await faults.FirstLineAsync.WaitAsync(Wait);

        result.ShouldSucceed();
        line.ShouldContain("unavailable");

        await session.Client.CallToolAsync("memory_stats", EmptyArgs()).AsTask().WaitAsync(Wait);
        await session.Client.ListToolsAsync().AsTask().WaitAsync(Wait);
        await Task.Delay(300);

        sessions.Attempts.ShouldBe(1);
        faults.Lines.Count.ShouldBe(1);
    }

    [Fact]
    public async Task PendingWrite_BlocksNeitherTheRequestNorTheShutdown()
    {
        var sessions = new FakeSessionRegistry { BlockWrites = true };
        var started = DateTimeOffset.UtcNow;
        var session = await StartAsync(
            new RecordingSessionCaptureFaultSink(),
            sessions: sessions,
            writeBudget: TimeSpan.FromSeconds(60));

        var call = await session.Client.CallToolAsync("memory_stats", EmptyArgs()).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5));

        call.ShouldSucceed();
        (DateTimeOffset.UtcNow - started).ShouldBeLessThan(
            TimeSpan.FromSeconds(5), "the request path never waits on the registry write");

        var shutdown = DateTimeOffset.UtcNow;
        await session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));
        (DateTimeOffset.UtcNow - shutdown).ShouldBeLessThan(
            TimeSpan.FromSeconds(15), "a pending registry write must not hold the session shutdown");
    }

    [Fact]
    public async Task WriteBudget_BoundsAPendingWrite_AndFaultsOnce()
    {
        var sessions = new FakeSessionRegistry { BlockWrites = true };
        var faults = new RecordingSessionCaptureFaultSink();
        var started = DateTimeOffset.UtcNow;

        await using var session = await StartAsync(
            faults,
            sessions: sessions,
            writeBudget: TimeSpan.FromMilliseconds(250));

        await session.Client.CallToolAsync("memory_stats", EmptyArgs()).AsTask().WaitAsync(Wait);
        var line = await faults.FirstLineAsync.WaitAsync(TimeSpan.FromSeconds(10));

        line.ShouldContain("write budget");
        (DateTimeOffset.UtcNow - started).ShouldBeLessThan(
            TimeSpan.FromSeconds(5), "the write is bounded by its budget, not by the store");
        faults.Lines.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ConnectionThatNeverSendsARequest_WritesNoRow()
    {
        var sessions = new FakeSessionRegistry();
        var clientToServer = Channel.CreateUnbounded<JsonRpcMessage>();
        var serverToClient = Channel.CreateUnbounded<JsonRpcMessage>();
        var services = new FakeServiceProvider(
            new FakeMemoryStore(), new FakeEmbeddingProvider(), new FakeClock(), sessions: sessions);
        await using var server = McpServerFactory.Create(
            new ChannelTransport(clientToServer.Reader, serverToClient.Writer),
            new McpHostOptions(),
            services);
        using var cancellation = new CancellationTokenSource();
        var run = Task.Run(() => server.RunAsync(cancellation.Token));

        await clientToServer.Writer.WriteAsync(new JsonRpcNotification
        {
            Method = "notifications/initialized",
        });
        await Task.Delay(500);

        sessions.Attempts.ShouldBe(0);

        clientToServer.Writer.TryComplete();
        serverToClient.Writer.TryComplete();
        await cancellation.CancelAsync();
        await run.WaitAsync(Wait);
    }

    private static async Task<McpTestSession> StartAsync(
        RecordingSessionCaptureFaultSink faults,
        FakeSessionRegistry? sessions = null,
        string? clientProtocolVersion = null,
        TimeSpan? writeBudget = null) =>
        await McpTestSession.StartAsync(
            new McpHostOptions
            {
                CaptureFault = faults,
                SessionWriteBudget = writeBudget,
            },
            sessions: sessions,
            clientProtocolVersion: clientProtocolVersion);

    private static Dictionary<string, object?> EmptyArgs() => new();

    private static Dictionary<string, object?> IdentityArgs(string alias, string? vendor)
    {
        var args = new Dictionary<string, object?> { ["alias"] = alias };
        if (vendor is not null)
        {
            args["vendor"] = vendor;
        }

        return args;
    }
}
