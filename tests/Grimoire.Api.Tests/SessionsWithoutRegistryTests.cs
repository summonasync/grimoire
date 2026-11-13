using System.Net;
using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public sealed class SessionsWithoutRegistryTests : IAsyncLifetime
{
    private ServiceProvider? _ports;
    private WebApplication? _app;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        _ports = new ServiceCollection()
            .AddSingleton<IMemoryStore>(new FakeMemoryStore())
            .AddSingleton<ITomeManifestStore>(new FakeTomeManifestStore())
            .AddSingleton<IEmbeddingProvider>(new FakeEmbeddingProvider())
            .AddSingleton<IClock>(new FakeClock())
            .AddSingleton<IProjectRegistry>(new ProjectRegistry([]))
            .AddSingleton<ICallerPathProvider>(new FixedCallerPath(GrimoireApiFactory.DefaultCallerPath))
            .AddSingleton(new GcOptions { SweepInterval = TimeSpan.FromMinutes(60) })
            .AddSingleton<IGcStateStore>(new FakeGcStateStore())
            .AddSingleton(new ConsolidationOptions { SweepInterval = TimeSpan.FromMinutes(60) })
            .AddSingleton<IConsolidationStateStore>(new FakeConsolidationStateStore())
            .BuildServiceProvider();

        _app = ApiHostBuilder.Build(new ApiHostOptions
        {
            Port = 0,
            GcIntervalMinutes = 60,
            StaticRootPath = Path.Combine(Path.GetTempPath(), "grimoire-api-tests", "sessions-without-registry"),
        }, _ports);
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        _client?.Dispose();
        _ports?.Dispose();
    }

    [Fact]
    public async Task Get_WithoutAWiredRegistry_AnswersTheStandardApiNotFound()
    {
        var response = await _client!.GetAsync("/api/sessions");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        (await response.Content.ReadAsStringAsync()).ShouldContain("not_found");
    }

    [Fact]
    public async Task Get_WithoutAWiredRegistry_LeavesTheRestOfTheApiServing()
    {
        var response = await _client!.GetAsync("/api/agents");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
