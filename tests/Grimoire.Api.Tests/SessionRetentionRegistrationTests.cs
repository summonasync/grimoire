using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public sealed class SessionRetentionRegistrationTests
{
    [Fact]
    public void Build_WithAWiredRegistry_AddsTheRetentionSweepBesideTheOtherSweeps()
    {
        using var app = ApiHostBuilder.Build(Options(), Ports(withRegistry: true));

        var hosted = app.Services.GetServices<IHostedService>().ToList();

        hosted.OfType<SessionRetentionSweeper>().ShouldHaveSingleItem();
        hosted.ShouldContain(service => service is GcSweeper);
        hosted.ShouldContain(service => service is ConsolidationSweeper);
    }

    [Fact]
    public void Build_WithoutARegistry_AddsNoRetentionSweep()
    {
        using var app = ApiHostBuilder.Build(Options(), Ports(withRegistry: false));

        app.Services.GetServices<IHostedService>()
            .ShouldNotContain(service => service is SessionRetentionSweeper);
    }

    private static ApiHostOptions Options() => new()
    {
        Port = 0,
        GcIntervalMinutes = 60,
        SessionsRetentionHours = 24,
        StaticRootPath = Path.Combine(Path.GetTempPath(), "grimoire-api-tests", "retention-registration"),
    };

    private static ServiceProvider Ports(bool withRegistry)
    {
        var services = new ServiceCollection()
            .AddSingleton<IMemoryStore>(new FakeMemoryStore())
            .AddSingleton<ITomeManifestStore>(new FakeTomeManifestStore())
            .AddSingleton<IEmbeddingProvider>(new FakeEmbeddingProvider())
            .AddSingleton<IClock>(new FakeClock())
            .AddSingleton<IProjectRegistry>(new ProjectRegistry([]))
            .AddSingleton<ICallerPathProvider>(new FixedCallerPath(GrimoireApiFactory.DefaultCallerPath))
            .AddSingleton(new GcOptions { SweepInterval = TimeSpan.FromMinutes(60) })
            .AddSingleton<IGcStateStore>(new FakeGcStateStore())
            .AddSingleton(new ConsolidationOptions { SweepInterval = TimeSpan.FromMinutes(60) })
            .AddSingleton<IConsolidationStateStore>(new FakeConsolidationStateStore());

        if (withRegistry)
        {
            services.AddSingleton<ISessionRegistry>(new FakeSessionRegistry());
        }

        return services.BuildServiceProvider();
    }
}
