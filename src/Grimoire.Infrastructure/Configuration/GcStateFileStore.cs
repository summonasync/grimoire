using System.Text.Json;
using Grimoire.Core;

namespace Grimoire.Infrastructure.Configuration;

public sealed class GcStateFileStore(string? path = null) : IGcStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".grimoire",
        "gc-state.json");

    private readonly string _path = path ?? DefaultPath;

    public Task<GcState> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Read());
    }

    public async Task WriteAsync(GcState state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(_path)}.tmp");
        await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(ToHeartbeat(state), JsonOptions), cancellationToken);
        File.Move(tempPath, _path, true);
    }

    private GcState Read()
    {
        if (!File.Exists(_path))
        {
            return GcState.Unknown("gc state file does not exist yet");
        }

        string text;
        try
        {
            text = File.ReadAllText(_path);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return GcState.Unknown($"gc state file could not be read: {failure.Message}");
        }

        try
        {
            var heartbeat = JsonSerializer.Deserialize<Heartbeat>(text, JsonOptions);
            return heartbeat is { LastAttemptAt: not null }
                ? new GcState
                {
                    LastAttemptAt = heartbeat.LastAttemptAt,
                    LastSuccessAt = heartbeat.LastSuccessAt,
                    RemovedExpired = heartbeat.RemovedExpired,
                    RemovedTombstones = heartbeat.RemovedTombstones,
                    LastError = heartbeat.LastError,
                }
                : GcState.Unknown("gc state file holds no recorded sweep attempts");
        }
        catch (JsonException failure)
        {
            return GcState.Unknown($"gc state file is not valid JSON: {failure.Message}");
        }
    }

    private static Heartbeat ToHeartbeat(GcState state) => new(
        state.LastAttemptAt,
        state.LastSuccessAt,
        state.RemovedExpired,
        state.RemovedTombstones,
        state.LastError);

    private sealed record Heartbeat(
        DateTimeOffset? LastAttemptAt,
        DateTimeOffset? LastSuccessAt,
        int? RemovedExpired,
        int? RemovedTombstones,
        string? LastError);
}
