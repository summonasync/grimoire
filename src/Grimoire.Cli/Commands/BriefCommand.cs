using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class BriefCommand(CliEnvironment environment)
{
    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var brief = services.GetRequiredService<BriefService>();
        var daily = CliValues.Switch(invocation, invocation.Spec, "daily");
        var result = await brief.BriefAsync(new BriefQuery
        {
            ProjectId = invocation.Value("project"),
            Scopes = CliValues.ScopeList(invocation.Spec, invocation.Value("scope")),
            FactsPerScope = Hint(invocation, "facts-per-scope"),
            JournalPerScope = Hint(invocation, "journal-per-scope"),
            TombstoneLimit = Hint(invocation, "tombstone-limit"),
            Daily = daily,
        }, cancellationToken);

        MemoryFormatter.WriteBrief(environment.Output, result, daily);
        return ExitCodes.Ok;
    }

    private static int? Hint(CliInvocation invocation, string name) =>
        invocation.Value(name) is { } raw ? CliValues.PositiveInt(invocation.Spec, name, raw) : null;
}
