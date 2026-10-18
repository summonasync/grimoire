using Grimoire.Cli.CommandLine;
using Grimoire.Cli.Configuration;
using Grimoire.Core;
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
        var claim = ParseClaim(invocation);

        var written = ConfigFile.WriteDefaultIfMissing(environment.ConfigPath);
        environment.Output.WriteLine(written
            ? $"Wrote default config to {environment.ConfigPath}"
            : $"Config already present at {environment.ConfigPath}, left untouched");

        var fingerprint = EmbeddingFingerprint.From(config.Embedding.Model, config.Embedding.Dimensions);
        await services.GetRequiredService<QdrantCollectionBootstrapper>().EnsureAsync(fingerprint, cancellationToken);
        environment.Output.WriteLine(
            $"Collection '{config.Qdrant.Collection}' is ready (embedding '{config.Embedding.Model}', {config.Embedding.Dimensions} dimensions)");

        if (claim is { } block)
        {
            WriteClaimBlock(block.Alias, block.Tome);
        }

        return ExitCodes.Ok;
    }

    private static (string Alias, string? Tome)? ParseClaim(CliInvocation invocation)
    {
        var agent = invocation.Value("agent");
        var tome = invocation.Value("tome");
        if (agent is null)
        {
            if (tome is not null)
            {
                throw new UsageException("--tome only applies together with --agent.", invocation.Spec);
            }

            return null;
        }

        if (string.IsNullOrWhiteSpace(agent))
        {
            throw new UsageException("--agent must not be empty.", invocation.Spec);
        }

        string? tomeKey = null;
        if (tome is { } candidate && !TomeKey.TryNormalize(candidate, out tomeKey))
        {
            throw new UsageException(
                $"--tome must be a lowercase kebab-case tome key; '{candidate}' is not a valid tome key.", invocation.Spec);
        }

        return (agent.Trim(), tomeKey);
    }

    private void WriteClaimBlock(string alias, string? tome)
    {
        var path = Path.Combine(environment.WorkingDirectory, ActorClaimProvider.FileName);
        var (current, existed) = ConfigFile.Read(path);
        var updated = ActorClaimBlockWriter.Upsert(current ?? string.Empty, alias, tome);
        if (updated == current)
        {
            environment.Output.WriteLine($"Actor claim already up to date in {path}");
            return;
        }

        ConfigFile.Write(path, updated);
        var tomeSuffix = tome is null ? string.Empty : $", tome '{tome}'";
        environment.Output.WriteLine(existed
            ? $"Rewrote the actor claim block in {path} (alias '{alias}'{tomeSuffix})"
            : $"Wrote the actor claim block to {path} (alias '{alias}'{tomeSuffix})");
    }
}
