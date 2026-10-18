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
        var identity = await ActorResolver.ResolveAsync(
            environment, null, null, config.DefaultTome,
            services.GetRequiredService<IActorClaimProvider>(), cancellationToken);
        var stats = await services.GetRequiredService<MemoryService>().StatsAsync(cancellationToken);
        environment.Output.WriteLine($"Active: {stats.Active} ({ScopeBreakdown(stats)})");
        environment.Output.WriteLine($"Superseded: {stats.Superseded}");
        environment.Output.WriteLine($"Tombstoned: {stats.Tombstoned}");
        environment.Output.WriteLine($"Pending compaction: {stats.PendingCompaction}");
        environment.Output.WriteLine($"Idempotency conflicts: {stats.IdempotencyConflicts}");
        environment.Output.WriteLine($"GC: {GcText(stats.Gc)}");
        environment.Output.WriteLine($"Tome: {identity.Tome} ({SourceText(identity.Source)})");
        environment.Output.WriteLine($"Per tome: {PerTome(stats)}");
        return ExitCodes.Ok;
    }

    private static string ScopeBreakdown(MemoryStats stats) => string.Join(
        ", ",
        MemoryScopes.All.Select(scope => $"{scope.ToString().ToLowerInvariant()}: {stats.ActiveByScope.GetValueOrDefault(scope)}"));

    private static string GcText(GcState gc) => gc.IsUnknown
        ? $"unknown ({gc.UnknownReason}), stale: {FormatFlag(gc.Stale)}"
        : $"last attempt {Timestamp(gc.LastAttemptAt)}, last success {Timestamp(gc.LastSuccessAt)}, " +
          $"removed {FormatCount(gc.RemovedExpired)} expired / {FormatCount(gc.RemovedTombstones)} tombstones, " +
          $"last error: {gc.LastError ?? "none"}, stale: {FormatFlag(gc.Stale)}";

    private static string Timestamp(DateTimeOffset? moment) => moment?.ToString("u") ?? "never";

    private static string FormatCount(int? count) => count?.ToString() ?? "?";

    private static string FormatFlag(bool flag) => flag ? "true" : "false";

    private static string PerTome(MemoryStats stats) => stats.ByTome.Count == 0
        ? "(none)"
        : string.Join(
            ", ",
            stats.ByTome
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key} ({pair.Value.Active} active, {pair.Value.Tombstoned} tombstoned)"));

    private static string SourceText(TomeSource source) => source switch
    {
        TomeSource.ExplicitParam => "explicit parameter",
        TomeSource.EnvTome => "env tome",
        TomeSource.EnvActor => "env actor",
        TomeSource.ClaimFile => "claim file",
        TomeSource.ClientInfo => "client info",
        _ => "default",
    };
}
