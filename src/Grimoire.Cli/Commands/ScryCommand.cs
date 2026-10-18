using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Mcp;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class ScryCommand(CliEnvironment environment)
{
    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var query = invocation.JoinedPositional();
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new UsageException("Scry needs a query to look for.", invocation.Spec);
        }

        var tomes = CliValues.TomeList(invocation.Spec, invocation.Value("tomes"));
        var includeJournal = CliValues.Switch(invocation, invocation.Spec, "include-journal");
        var attribution = CliValues.Attribution(invocation.Spec, invocation.Value("attribution"));
        var limit = invocation.Value("limit") is { } raw ? CliValues.PositiveInt(invocation.Spec, "limit", raw) : 5;

        var identity = await ActorResolver.ResolveAsync(
            environment, null, null, config.DefaultTome,
            services.GetRequiredService<IActorClaimProvider>(), cancellationToken);

        var targets = tomes.Count > 0
            ? tomes.Distinct().OrderBy(tome => tome, StringComparer.Ordinal).ToArray()
            : [.. (await DiscoverTomesAsync(services, cancellationToken)).Where(tome => tome != identity.Tome)];

        if (targets.Length == 0)
        {
            environment.Output.WriteLine("No other tomes to scry.");
            return ExitCodes.Ok;
        }

        var matches = await services.GetRequiredService<MemoryService>().RecallAsync(new RecallQuery
        {
            Text = query,
            Scopes = MemoryScopes.LongTerm,
            Kind = includeJournal ? null : MemoryKind.Fact,
            Tomes = targets,
            Limit = limit,
        }, cancellationToken);

        environment.Output.WriteLine($"Tomes: {string.Join(", ", targets)}");
        if (matches.Count == 0)
        {
            environment.Output.WriteLine("No matching memories in other tomes.");
            return ExitCodes.Ok;
        }

        foreach (var match in matches)
        {
            MemoryFormatter.WriteScryMatch(environment.Output, match, AttributionText(match, attribution));
        }

        return ExitCodes.Ok;
    }

    private static async Task<IReadOnlyList<string>> DiscoverTomesAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var records = await services.GetRequiredService<IMemoryStore>()
            .ListAsync(new MemoryFilter { Status = MemoryStatus.Active }, cancellationToken);
        return [.. records.Select(record => record.Tome).Distinct().OrderBy(tome => tome, StringComparer.Ordinal)];
    }

    private static string? AttributionText(RecallMatch match, ScryAttribution attribution) => attribution switch
    {
        ScryAttribution.Light => match.Record.Actor.Alias,
        ScryAttribution.Full => match.Record.Actor.ToString(),
        _ => null,
    };
}
