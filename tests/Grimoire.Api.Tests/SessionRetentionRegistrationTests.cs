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

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Build_WithNonFiniteRetentionHours_RefusesTheHostBeforeItBindsAPort(double hours)
    {
        var failure = Should.Throw<ArgumentOutOfRangeException>(
            () => ApiHostBuilder.Build(Options(hours), Ports(withRegistry: true)));

        failure.ParamName.ShouldBe("retentionHours");
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-5d)]
    public void Build_WithANonPositiveRetentionHours_RejectsTheSweepAtConstruction(double hours)
    {
        using var app = ApiHostBuilder.Build(Options(hours), Ports(withRegistry: true));

        var failure = Should.Throw<ArgumentOutOfRangeException>(
            () => app.Services.GetServices<IHostedService>().ToList());

        failure.ParamName.ShouldBe("retention");
    }

    private static ApiHostOptions Options(double? retentionHours = null) => new()
    {
        Port = 0,
        GcIntervalMinutes = 60,
        SessionsRetentionHours = retentionHours ?? 24,
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
