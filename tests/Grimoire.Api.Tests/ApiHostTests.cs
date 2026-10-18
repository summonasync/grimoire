using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class ApiHostTests
{
    [Fact]
    public async Task Run_WithHostileConfigEndpoints_FailsFastAfterBind()
    {
        var services = new ServiceCollection()
            .AddSingleton<IMemoryStore>(new FakeMemoryStore())
            .AddSingleton<IEmbeddingProvider>(new FakeEmbeddingProvider())
            .AddSingleton<IClock>(new FakeClock())
            .BuildServiceProvider();

        Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Endpoints__Evil__Url", "http://0.0.0.0:5999");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var runTask = ApiHost.RunAsync(new ApiHostOptions
            {
                Port = 0,
                StaticRootPath = Path.Combine(Path.GetTempPath(), "grimoire-api-tests", "post-bind-guard"),
                DisableConfigSourceClear = true,
            }, services, cts.Token);

            var error = await Should.ThrowAsync<InvalidOperationException>(() => runTask);
            error.Message.ShouldContain("http://0.0.0.0:5999");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Endpoints__Evil__Url", null);
        }
    }
}
