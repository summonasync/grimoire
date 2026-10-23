using Grimoire.Cli.CommandLine;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class DropCollectionCommand(CliEnvironment environment)
{
    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (invocation.Value("yes") is null)
        {
            throw new UsageException(
                $"Dropping '{config.Qdrant.Collection}' deletes every memory in the store and cannot be undone. " +
                "Pass --yes to confirm.",
                invocation.Spec);
        }

        var db = services.GetRequiredService<IQdrantVectorDb>();
        if (!await db.CollectionExistsAsync(config.Qdrant.Collection, cancellationToken))
        {
            environment.Output.WriteLine($"Collection '{config.Qdrant.Collection}' does not exist, nothing to drop");
            return ExitCodes.Ok;
        }

        await db.DropCollectionAsync(config.Qdrant.Collection, cancellationToken);
        environment.Output.WriteLine($"Dropped collection '{config.Qdrant.Collection}'");
        return ExitCodes.Ok;
    }
}
