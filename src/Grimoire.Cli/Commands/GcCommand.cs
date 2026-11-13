using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class GcCommand(CliEnvironment environment)
{
    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var result = await services.GetRequiredService<MemoryService>().GcAsync(cancellationToken);
        environment.Output.WriteLine(
            $"Removed {result.TotalRemoved} memories (expired sessions: {result.ExpiredSessions}, " +
            $"aged tombstones: {result.AgedTombstones}, past TTL kept by policy: {result.RetainedPastTtl})");
        return ExitCodes.Ok;
    }
}
