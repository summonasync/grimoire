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
        var matches = await memories.RecallAsync(new RecallQuery
        {
            Text = invocation.JoinedPositional(),
            Scopes = CliValues.ScopeList(invocation.Spec, invocation.Value("scope")),
            Limit = invocation.Value("limit") is { } raw ? CliValues.PositiveInt(invocation.Spec, "limit", raw) : 5,
        }, cancellationToken);

        if (matches.Count == 0)
        {
            environment.Output.WriteLine("No matching memories.");
            return ExitCodes.Ok;
        }

        foreach (var match in matches)
        {
            MemoryFormatter.WriteMatch(environment.Output, match);
        }

        return ExitCodes.Ok;
    }
}
