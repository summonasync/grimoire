using System.Threading.Channels;
using Grimoire.Core;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

internal sealed class McpTestSession : IAsyncDisposable
{
    public const string ClientName = "test-client";

    public const string ClientActor = ClientName;

    private readonly Channel<JsonRpcMessage> _clientToServer;

    private readonly Channel<JsonRpcMessage> _serverToClient;

    private readonly CancellationTokenSource _cancellation;

    private readonly Task _serverRun;

    private McpTestSession(
        FakeMemoryStore store,
        FakeEmbeddingProvider embeddings,
        FakeClock clock,
        FakeGcStateStore gcState,
        FakeConsolidationStateStore consolidationState,
        FakeProjectRegistryStore registry,
        McpClient client,
        Channel<JsonRpcMessage> clientToServer,
        Channel<JsonRpcMessage> serverToClient,
        Task serverRun,
        CancellationTokenSource cancellation)
    {
        Store = store;
        Embeddings = embeddings;
        Clock = clock;
        Gc = gcState;
        Consolidation = consolidationState;
        Registry = registry;
        Client = client;
        _clientToServer = clientToServer;
        _serverToClient = serverToClient;
        _serverRun = serverRun;
        _cancellation = cancellation;
    }

    public FakeMemoryStore Store { get; }

    public FakeEmbeddingProvider Embeddings { get; }

    public FakeClock Clock { get; }

    public FakeGcStateStore Gc { get; }

    public FakeConsolidationStateStore Consolidation { get; }

    public FakeProjectRegistryStore Registry { get; }

    public McpClient Client { get; }

    public static IReadOnlyList<ProjectAliasRecord> DefaultProjects { get; } =
    [
        new ProjectAliasRecord { Alias = "grimoire", Paths = ["/src/grimoire"] },
        new ProjectAliasRecord { Alias = "alpha", Paths = ["/src/alpha"], BoardProjectId = "board-1", DisplayName = "Alpha" },
        new ProjectAliasRecord { Alias = "beta", Paths = ["/src/beta"] },
    ];

    public static async Task<McpTestSession> StartAsync(
        McpHostOptions? hostOptions = null,
        MemoryEngineOptions? engineOptions = null,
        IActorClaimProvider? claims = null,
        GcOptions? gcOptions = null,
        FakeGcStateStore? gcState = null,
        FakeConsolidationStateStore? consolidationState = null,
        string? callerPath = null,
        IReadOnlyList<ProjectAliasRecord>? projects = null,
        FakeProjectRegistryStore? registryStore = null,
        FakeBoardProjectSource? board = null,
        IAgentIdentitySource? identityFile = null)
    {
        var store = new FakeMemoryStore();
        var embeddings = new FakeEmbeddingProvider();
        var clock = new FakeClock();
        var gc = gcState ?? new FakeGcStateStore();
        var consolidation = consolidationState ?? new FakeConsolidationStateStore();
        var registry = registryStore ?? new FakeProjectRegistryStore(projects ?? DefaultProjects);
        var services = new FakeServiceProvider(
            store, embeddings, clock, engineOptions, claims, gcOptions, gc,
            callerPath is null ? null : new FakeCallerPathProvider(callerPath),
            registry,
            board,
            consolidation,
            identityFile);
        var options = hostOptions ?? new McpHostOptions();

        var clientToServer = Channel.CreateUnbounded<JsonRpcMessage>();
        var serverToClient = Channel.CreateUnbounded<JsonRpcMessage>();


        var serverTransport = new ChannelTransport(clientToServer.Reader, serverToClient.Writer);
        var clientTransport = new ChannelClientTransport(serverToClient.Reader, clientToServer.Writer);

        var server = McpServerFactory.Create(serverTransport, options, services);
        var cancellation = new CancellationTokenSource();
        var serverRun = Task.Run(() => server.RunAsync(cancellation.Token));

        var client = await Task.Run(() => McpClient.CreateAsync(clientTransport, new McpClientOptions
        {
            ClientInfo = new Implementation { Name = ClientName, Version = "0.1.0" },
        })).WaitAsync(TimeSpan.FromSeconds(15));

        return new McpTestSession(
            store, embeddings, clock, gc, consolidation, registry, client, clientToServer, serverToClient, serverRun, cancellation);
    }

    public async ValueTask DisposeAsync()
    {
        _clientToServer.Writer.TryComplete();
        _serverToClient.Writer.TryComplete();

        await Client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        await _cancellation.CancelAsync();

        try
        {
            await _serverRun.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (OperationCanceledException)
        {
        }
        catch (TimeoutException)
        {
        }
    }
}
