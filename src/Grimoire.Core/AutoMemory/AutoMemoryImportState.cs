using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Grimoire.Core;

public sealed record AutoMemoryImportState
{
    private const string MarkerFileName = ".grimoire-import-state.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public required DateTimeOffset ImportedAt { get; init; }

    public required string SourcePath { get; init; }

    public required IReadOnlyDictionary<string, string> Hashes { get; init; }

    public static string PathFor(string sourceDirectory) => Path.Combine(sourceDirectory, MarkerFileName);

    public static async Task<AutoMemoryImportStateRead> ReadAsync(string sourceDirectory, CancellationToken cancellationToken)
    {
        var path = PathFor(sourceDirectory);
        if (!File.Exists(path))
        {
            return new AutoMemoryImportStateRead();
        }

        string text;
        try
        {
            text = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return new AutoMemoryImportStateRead { Warning = $"import marker could not be read: {failure.Message}" };
        }

        try
        {
            var marker = JsonSerializer.Deserialize<Marker>(text, JsonOptions);
            return marker is { ImportedAt: not null, SourcePath: not null, Hashes: not null }
                ? new AutoMemoryImportStateRead
                {
                    State = new AutoMemoryImportState
                    {
                        ImportedAt = marker.ImportedAt.Value,
                        SourcePath = marker.SourcePath,
                        Hashes = marker.Hashes,
                    },
                }
                : new AutoMemoryImportStateRead { Warning = "import marker is missing required fields" };
        }
        catch (JsonException failure)
        {
            return new AutoMemoryImportStateRead { Warning = $"import marker is not valid JSON: {failure.Message}" };
        }
    }

    public static async Task WriteAsync(string sourceDirectory, AutoMemoryImportState state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(sourceDirectory);
        var path = PathFor(sourceDirectory);
        var tempPath = Path.Combine(sourceDirectory, $".{MarkerFileName}.tmp");

        try
        {
            await File.WriteAllTextAsync(
                tempPath,
                JsonSerializer.Serialize(ToMarker(state), JsonOptions),
                cancellationToken);
            File.Move(tempPath, path, true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public static string ContentHash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private static Marker ToMarker(AutoMemoryImportState state) => new(state.ImportedAt, state.SourcePath, state.Hashes);

    private sealed record Marker(
        DateTimeOffset? ImportedAt,
        string? SourcePath,
        IReadOnlyDictionary<string, string>? Hashes);
}
