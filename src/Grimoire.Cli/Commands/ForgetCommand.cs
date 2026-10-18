using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class ForgetCommand(CliEnvironment environment)
{
    private const int MatchProbeLimit = 5;

    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var memories = services.GetRequiredService<MemoryService>();
        var reason = invocation.Value("reason") ?? "forgotten via grimoire cli";
        var target = invocation.JoinedPositional();

        return Guid.TryParse(target, out var id)
            ? await ForgetByIdAsync(memories, id, reason, cancellationToken)
            : await ForgetByQueryAsync(services, memories, target, reason, cancellationToken);
    }

    private async Task<int> ForgetByIdAsync(MemoryService memories, Guid id, string reason, CancellationToken cancellationToken)
    {
        if (!await memories.ForgetAsync(id, reason, cancellationToken))
        {
            throw new CommandException("not_found", $"No forgettable memory '{id}' (missing or already tombstoned).", ExitCodes.NotFound);
        }

        environment.Output.WriteLine($"Tombstoned {id}: {reason}");
        return ExitCodes.Ok;
    }

    private async Task<int> ForgetByQueryAsync(
        IServiceProvider services,
        MemoryService memories,
        string query,
        string reason,
        CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<IMemoryStore>();
        var embeddings = services.GetRequiredService<IEmbeddingProvider>();
        var options = services.GetRequiredService<MemoryEngineOptions>();

        var vector = await embeddings.EmbedAsync(query, cancellationToken);
        var matches = await store.SearchAsync(
            vector,
            new MemoryFilter { Status = MemoryStatus.Active },
            MatchProbeLimit,
            cancellationToken);
        var best = matches
            .Where(match => match.Similarity >= options.DedupSimilarityThreshold)
            .OrderByDescending(match => match.Similarity)
            .FirstOrDefault();

        if (best is null)
        {
            var closest = matches.Count == 0 ? "no candidates" : $"closest similarity {matches[0].Similarity:F3}";
            throw new CommandException("not_found", $"No active memory close to '{query}' ({closest}).", ExitCodes.NotFound);
        }

        await memories.ForgetAsync(best.Record.Id, reason, cancellationToken);
        environment.Output.WriteLine($"Tombstoned {best.Record.Id} (similarity {best.Similarity:F3}): {FirstLine(best.Record.Content)}");
        return ExitCodes.Ok;
    }

    private static string FirstLine(string content)
    {
        var newline = content.IndexOf('\n');
        return newline < 0 ? content : content[..newline].TrimEnd('\r');
    }
}
