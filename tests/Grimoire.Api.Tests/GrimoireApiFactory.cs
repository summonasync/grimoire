using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Api.Tests;

public sealed class GrimoireApiFactory : IDisposable
{
    public const string DefaultCallerPath = "/agents/api-tests";

    private readonly MemoryEngineOptions? _engineOptions;
    private readonly string? _staticRootPath;
    private readonly string? _envTome;
    private readonly string? _envActor;
    private readonly IReadOnlyList<ProjectAliasRecord>? _projects;
    private readonly string? _callerPath;
    private readonly IAgentIdentitySource? _identityFiles;
    private ServiceProvider? _ports;
    private WebApplication? _app;

    public GrimoireApiFactory()
    {
    }

    private GrimoireApiFactory(
        MemoryEngineOptions? engineOptions,
        string? staticRootPath,
        string? envTome,
        string? envActor,
        IReadOnlyList<ProjectAliasRecord>? projects,
        string? callerPath,
        IAgentIdentitySource? identityFiles)
    {
        _engineOptions = engineOptions;
        _staticRootPath = staticRootPath;
        _envTome = envTome;
        _envActor = envActor;
        _projects = projects;
        _callerPath = callerPath;
        _identityFiles = identityFiles;
    }

    public static GrimoireApiFactory Create(
        MemoryEngineOptions? engineOptions = null,
        string? staticRootPath = null,
        string? envTome = null,
        string? envActor = null,
        IReadOnlyList<ProjectAliasRecord>? projects = null,
        string? callerPath = null,
        IAgentIdentitySource? identityFiles = null) =>
        new(engineOptions, staticRootPath, envTome, envActor, projects, callerPath, identityFiles);

    public FakeMemoryStore Store { get; } = new();

    public FakeEmbeddingProvider Embeddings { get; } = new();

    public FakeClock Clock { get; } = new();

    public FakeGcStateStore GcState { get; } = new();

    public FakeConsolidationStateStore ConsolidationState { get; } = new();

    public void Reset()
    {
        Store.Clear();
        Store.UpsertCount = 0;
        Store.UpdateCount = 0;
        Store.GetCount = 0;
        Store.OnGetAsync = null;
        Store.OnFetchAsync = null;
        Embeddings.Requests.Clear();
        Embeddings.Failure = null;
        GcState.Reset();
        ConsolidationState.Reset();
    }

    public HttpClient NewClient()
    {
        var app = HostAsync().GetAwaiter().GetResult();
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    public void Dispose()
    {
        _app?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _ports?.Dispose();
    }

    private async Task<WebApplication> HostAsync()
    {
        if (_app is not null)
        {
            return _app;
        }

        _app = ApiHostBuilder.Build(new ApiHostOptions
        {
            Port = 0,
            GcIntervalMinutes = 60,
            StaticRootPath = _staticRootPath ?? Path.Combine(Path.GetTempPath(), "grimoire-api-tests", "missing-export"),
            EnvTome = _envTome,
            EnvActor = _envActor,
            IdentityFile = _identityFiles,
        }, Ports());
        await _app.StartAsync();
        return _app;
    }

    private IServiceProvider Ports()
    {
        if (_ports is not null)
        {
            return _ports;
        }

        var services = new ServiceCollection();
        services.AddSingleton<IMemoryStore>(Store);
        services.AddSingleton<IEmbeddingProvider>(Embeddings);
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IProjectRegistry>(new ProjectRegistry(_projects ?? []));
        services.AddSingleton<ICallerPathProvider>(new FixedCallerPath(_callerPath ?? DefaultCallerPath));
        services.AddSingleton(new GcOptions { SweepInterval = TimeSpan.FromMinutes(60) });
        services.AddSingleton<IGcStateStore>(GcState);
        services.AddSingleton(new ConsolidationOptions { SweepInterval = TimeSpan.FromMinutes(60) });
        services.AddSingleton<IConsolidationStateStore>(ConsolidationState);
        if (_engineOptions is not null)
        {
            services.AddSingleton(_engineOptions);
        }

        _ports = services.BuildServiceProvider();
        return _ports;
    }
}
