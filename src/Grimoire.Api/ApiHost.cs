using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Grimoire.Api;

public static class ApiHost
{
    public static async Task<int> RunAsync(ApiHostOptions options, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var app = ApiHostBuilder.Build(options, services);
        await app.StartAsync(cancellationToken);
        try
        {
            LoopbackGuard.EnsureLoopbackOnly(app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()?.Addresses ?? []);
            await app.WaitForShutdownAsync(cancellationToken);
        }
        finally
        {
            await app.StopAsync(CancellationToken.None);
        }

        return 0;
    }
}
