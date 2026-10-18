using Grimoire.Cli.CommandLine;
using Grimoire.Cli.Export;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class ImportCommand(CliEnvironment environment)
{
    private const int BatchSize = 256;

    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(invocation.Positional[0], environment.WorkingDirectory);
        var store = services.GetRequiredService<IMemoryStore>();
        var current = EmbeddingFingerprint.From(config.Embedding.Model, config.Embedding.Dimensions);
        var total = await CountEntriesAsync(path, cancellationToken);
        var imported = 0;

        await using (var input = ExportFile.OpenRead(path))
        {
            var reader = new StoreImportReader(new StreamReader(input));
            RefuseModelMismatch(await reader.ReadHeaderAsync(cancellationToken), current, path);

            var batch = new List<MemoryPoint>(BatchSize);

            await foreach (var entry in reader.ReadEntriesAsync(cancellationToken))
            {
                batch.Add(new MemoryPoint { Record = entry.Record, Vector = entry.Vector });

                if (batch.Count < BatchSize)
                {
                    continue;
                }

                imported += batch.Count;
                await FlushAsync(store, batch, cancellationToken);
                environment.Output.WriteLine($"{imported}/{total} imported");
            }

            if (batch.Count > 0)
            {
                imported += batch.Count;
                await FlushAsync(store, batch, cancellationToken);
            }
        }

        environment.Output.WriteLine($"Import complete: {imported}/{total} records from '{path}'");
        return ExitCodes.Ok;
    }

    private static async Task<int> CountEntriesAsync(string path, CancellationToken cancellationToken)
    {
        using var counter = new StreamReader(ExportFile.OpenRead(path));
        return await StoreImportReader.CountEntriesAsync(counter, cancellationToken);
    }

    private static async Task FlushAsync(IMemoryStore store, List<MemoryPoint> batch, CancellationToken cancellationToken)
    {
        await store.UpsertBatchAsync(batch, cancellationToken);
        batch.Clear();
    }

    private static void RefuseModelMismatch(ExportHeader header, EmbeddingFingerprint current, string path)
    {
        if (!header.Fingerprint.Equals(current))
        {
            throw new ReindexRequiredException(
                $"Export '{path}' was written for the '{header.Fingerprint.Model}' model ({header.Fingerprint.Dimensions} dimensions), " +
                $"but the store is configured for '{current.Model}' ({current.Dimensions} dimensions) — run `grimoire reindex` first, " +
                "then import an export that matches the configured model.");
        }
    }
}
