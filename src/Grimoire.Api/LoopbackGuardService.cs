using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;

namespace Grimoire.Api;

internal sealed class LoopbackGuardService(IServer server) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        LoopbackGuard.EnsureLoopbackOnly(server.Features.Get<IServerAddressesFeature>()?.Addresses ?? []);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
