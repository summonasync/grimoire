using System.Globalization;
using Grimoire.Cli.CommandLine;
using Grimoire.Cli.Export;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class ExportCommand(CliEnvironment environment)
{
    private const int ProgressInterval = 512;

    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var gzip = invocation.Value("gzip") is not null;
        var moment = services.GetRequiredService<IClock>().UtcNow;
        var path = ResolvePath(environment, invocation.Positional.FirstOrDefault(), gzip, moment);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var store = services.GetRequiredService<IMemoryStore>();
        var fingerprint = EmbeddingFingerprint.From(config.Embedding.Model, config.Embedding.Dimensions);
        var exported = 0;

        await using (var output = new StreamWriter(ExportFile.Create(path, gzip)))
        {
            exported = await new StoreExportWriter(output).WriteAsync(
                fingerprint,
                moment,
                store.EnumerateAllAsync(cancellationToken),
                count =>
                {
                    if (count % ProgressInterval == 0)
                    {
                        environment.Output.WriteLine($"{count} exported");
                    }
                },
                cancellationToken);
        }

        environment.Output.WriteLine($"Export complete: {exported} records to '{path}'");
        return ExitCodes.Ok;
    }

    internal static string ResolvePath(CliEnvironment environment, string? given, bool gzip, DateTimeOffset moment) =>
        given is { } explicitPath
            ? Path.GetFullPath(explicitPath, environment.WorkingDirectory)
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".grimoire",
                $"export-{Stamp(moment)}{(gzip ? ".jsonl.gz" : ".jsonl")}");

    private static string Stamp(DateTimeOffset moment) => moment.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture);
}
