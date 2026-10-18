using System.Net;
using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class LoopbackBindingTests
{
    [Fact]
    public void Endpoint_IsLoopbackAddress()
    {
        var endpoint = LoopbackBinding.Endpoint(4770);

        endpoint.Address.ShouldBe(IPAddress.Loopback);
        endpoint.Address.ToString().ShouldBe("127.0.0.1");
        endpoint.Port.ShouldBe(4770);
    }

    [Fact]
    public void Endpoint_NeverLeavesLoopbackFamily()
    {
        foreach (var port in new[] { 0, 1, 4770, ushort.MaxValue })
        {
            var endpoint = LoopbackBinding.Endpoint(port);
            IPAddress.IsLoopback(endpoint.Address).ShouldBeTrue($"port {port} must bind a loopback address");
        }
    }
}

public class ApiHostBuilderTests
{
    [Fact]
    public async Task Build_BindsLoopbackOnly()
    {
        var services = new ServiceCollection()
            .AddSingleton<IMemoryStore>(new FakeMemoryStore())
            .AddSingleton<IEmbeddingProvider>(new FakeEmbeddingProvider())
            .AddSingleton<IClock>(new FakeClock())
            .BuildServiceProvider();

        using var app = ApiHostBuilder.Build(new ApiHostOptions
        {
            Port = 0,
            StaticRootPath = Path.Combine(Path.GetTempPath(), "grimoire-api-tests", "loopback-proof"),
        }, services);

        await app.StartAsync();
        try
        {
            var addresses = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses;

            addresses.ShouldNotBeEmpty();
            foreach (var address in addresses)
            {
                var uri = new Uri(address);
                IPAddress.IsLoopback(IPAddress.Parse(uri.Host)).ShouldBeTrue($"'{address}' is not a loopback bind");
                uri.Host.ShouldBe("127.0.0.1");
            }
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Theory]
    [InlineData("http://127.0.0.2:5999")]
    [InlineData("http://0.0.0.0:5999")]
    public async Task Build_IgnoresHostileKestrelEndpointEnvVars(string hostileUrl)
    {
        var services = new ServiceCollection()
            .AddSingleton<IMemoryStore>(new FakeMemoryStore())
            .AddSingleton<IEmbeddingProvider>(new FakeEmbeddingProvider())
            .AddSingleton<IClock>(new FakeClock())
            .BuildServiceProvider();

        Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Endpoints__Evil__Url", hostileUrl);
        Environment.SetEnvironmentVariable("Kestrel__Endpoints__Evil__Url", hostileUrl);
        try
        {
            using var app = ApiHostBuilder.Build(new ApiHostOptions
            {
                Port = 0,
                StaticRootPath = Path.Combine(Path.GetTempPath(), "grimoire-api-tests", "hostile-env"),
            }, services);

            await app.StartAsync();
            try
            {
                var addresses = app.Services.GetRequiredService<IServer>()
                    .Features.Get<IServerAddressesFeature>()!.Addresses;

                addresses.ShouldNotBeEmpty();
                addresses.ShouldNotContain(address => address.Contains("5999") || address.Contains("0.0.0.0"));
                foreach (var address in addresses)
                {
                    var uri = new Uri(address);
                    uri.Host.ShouldBe("127.0.0.1");
                }
            }
            finally
            {
                await app.StopAsync();
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Endpoints__Evil__Url", null);
            Environment.SetEnvironmentVariable("Kestrel__Endpoints__Evil__Url", null);
        }
    }

    [Fact]
    public async Task Start_NonLoopbackAddressInServerFeatures_FailsFast()
    {
        var services = new ServiceCollection()
            .AddSingleton<IMemoryStore>(new FakeMemoryStore())
            .AddSingleton<IEmbeddingProvider>(new FakeEmbeddingProvider())
            .AddSingleton<IClock>(new FakeClock())
            .BuildServiceProvider();

        using var app = ApiHostBuilder.Build(new ApiHostOptions
        {
            Port = 0,
            StaticRootPath = Path.Combine(Path.GetTempPath(), "grimoire-api-tests", "guard-proof"),
        }, services);

        var feature = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!;
        feature.Addresses.Add("http://0.0.0.0:58113");
        feature.PreferHostingUrls = true;

        try
        {
            var error = await Should.ThrowAsync<InvalidOperationException>(() => app.StartAsync());

            error.Message.ShouldContain("http://0.0.0.0:58113");
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
