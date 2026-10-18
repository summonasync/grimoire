using System.Text.Json;
using Grimoire.Core;
using Grimoire.Infrastructure.Memory;

namespace Grimoire.Cli.Export;

internal sealed class StoreExportWriter(TextWriter writer)
{
    public async Task<int> WriteAsync(
        EmbeddingFingerprint fingerprint,
        DateTimeOffset exportedAt,
        IAsyncEnumerable<MemoryPoint> points,
        Action<int>? progress,
        CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync(
            JsonSerializer.Serialize(
                new ExportHeader
                {
                    FormatVersion = ExportFormat.Version,
                    Fingerprint = fingerprint,
                    ExportedAt = exportedAt,
                },
                ExportFormat.Json).AsMemory(),
            cancellationToken);

        var count = 0;

        await foreach (var point in points.WithCancellation(cancellationToken))
        {
            await writer.WriteLineAsync(
                JsonSerializer.Serialize(
                    new ExportEntry { Record = point.Record, Vector = point.Vector.ToArray() },
                    ExportFormat.Json).AsMemory(),
                cancellationToken);
            count++;
            progress?.Invoke(count);
        }

        return count;
    }
}
