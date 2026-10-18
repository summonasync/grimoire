using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Api.Tests;

public sealed class GrimoireApiFactory : IDisposable
{
    private readonly MemoryEngineOptions? _engineOptions;
    private readonly string? _staticRootPath;
    private ServiceProvider? _ports;
    private WebApplication? _app;

    public GrimoireApiFactory()
    {
    }

    private GrimoireApiFactory(MemoryEngineOptions? engineOptions, string? staticRootPath)
    {
        _engineOptions = engineOptions;
        _staticRootPath = staticRootPath;
    }

    public static GrimoireApiFactory Create(MemoryEngineOptions? engineOptions = null, string? staticRootPath = null) =>
        new(engineOptions, staticRootPath);

    public FakeMemoryStore Store { get; } = new();

    public FakeEmbeddingProvider Embeddings { get; } = new();

    public FakeClock Clock { get; } = new();

    public void Reset()
    {
        Store.Clear();
        Store.UpsertCount = 0;
        Store.UpdateCount = 0;
        Store.GetCount = 0;
        Store.OnGetAsync = null;
        Embeddings.Requests.Clear();
        Embeddings.Failure = null;
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
        if (_engineOptions is not null)
        {
            services.AddSingleton(_engineOptions);
        }

        _ports = services.BuildServiceProvider();
        return _ports;
    }
}
