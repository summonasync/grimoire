using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class ListCommand(CliEnvironment environment)
{
    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var memories = services.GetRequiredService<MemoryService>();
        var identity = await ActorResolver.ResolveAsync(
            environment, null, null,
            services.GetRequiredService<MemoryEngineOptions>().DefaultTome,
            services.GetRequiredService<IActorClaimProvider>(),
            services.GetService<IAgentIdentitySource>(), cancellationToken);
        var records = await memories.ListAsync(new MemoryFilter
        {
            Tome = identity.Tome,
            Scopes = CliValues.ScopeList(invocation.Spec, invocation.Value("scope")),
            Status = CliValues.Status(invocation.Spec, invocation.Value("status"), MemoryStatus.Active),
            Tags = CliValues.Tags(invocation.Value("tag")),
            Limit = invocation.Value("limit") is { } raw ? CliValues.PositiveInt(invocation.Spec, "limit", raw) : null,
        }, cancellationToken);

        if (records.Count == 0)
        {
            environment.Output.WriteLine("No memories.");
            return ExitCodes.Ok;
        }

        foreach (var record in records)
        {
            MemoryFormatter.WriteRecord(environment.Output, record);
        }

        return ExitCodes.Ok;
    }
}
