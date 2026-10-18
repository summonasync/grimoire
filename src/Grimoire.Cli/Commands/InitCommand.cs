using Grimoire.Cli.CommandLine;
using Grimoire.Cli.Configuration;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class InitCommand(CliEnvironment environment)
{
    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var written = ConfigFile.WriteDefaultIfMissing(environment.ConfigPath);
        environment.Output.WriteLine(written
            ? $"Wrote default config to {environment.ConfigPath}"
            : $"Config already present at {environment.ConfigPath}, left untouched");

        var fingerprint = EmbeddingFingerprint.From(config.Embedding.Model, config.Embedding.Dimensions);
        await services.GetRequiredService<QdrantCollectionBootstrapper>().EnsureAsync(fingerprint, cancellationToken);
        environment.Output.WriteLine(
            $"Collection '{config.Qdrant.Collection}' is ready (embedding '{config.Embedding.Model}', {config.Embedding.Dimensions} dimensions)");
        return ExitCodes.Ok;
    }
}
