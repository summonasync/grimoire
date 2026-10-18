using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class StatsCommand(CliEnvironment environment)
{
    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var stats = await services.GetRequiredService<MemoryService>().StatsAsync(cancellationToken);
        environment.Output.WriteLine($"Active: {stats.Active} ({ScopeBreakdown(stats)})");
        environment.Output.WriteLine($"Superseded: {stats.Superseded}");
        environment.Output.WriteLine($"Tombstoned: {stats.Tombstoned}");
        environment.Output.WriteLine($"Pending compaction: {stats.PendingCompaction}");
        environment.Output.WriteLine($"Idempotency conflicts: {stats.IdempotencyConflicts}");
        return ExitCodes.Ok;
    }

    private static string ScopeBreakdown(MemoryStats stats) => string.Join(
        ", ",
        MemoryScopes.All.Select(scope => $"{scope.ToString().ToLowerInvariant()}: {stats.ActiveByScope.GetValueOrDefault(scope)}"));
}
