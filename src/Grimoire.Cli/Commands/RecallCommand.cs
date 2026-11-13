using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class RecallCommand(CliEnvironment environment)
{
    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var memories = services.GetRequiredService<MemoryService>();
        var recall = await memories.RecallAsync(new RecallQuery
        {
            Text = invocation.JoinedPositional(),
            Scopes = CliValues.ScopeList(invocation.Spec, invocation.Value("scope")),
            Limit = invocation.Value("limit") is { } raw ? CliValues.PositiveInt(invocation.Spec, "limit", raw) : 5,
        }, cancellationToken);

        if (recall.Matches.Count == 0)
        {
            environment.Output.WriteLine("No matching memories.");
        }

        foreach (var match in recall.Matches)
        {
            MemoryFormatter.WriteMatch(environment.Output, match);
        }

        MemoryFormatter.WriteWarnings(environment.Output, recall.Warnings);
        return ExitCodes.Ok;
    }
}
