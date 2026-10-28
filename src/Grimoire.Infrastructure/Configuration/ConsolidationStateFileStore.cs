using System.Text.Json;
using Grimoire.Core;

namespace Grimoire.Infrastructure.Configuration;

public sealed class ConsolidationStateFileStore(string? path = null) : IConsolidationStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".grimoire",
        "consolidation-state.json");

    private readonly string _path = path ?? DefaultPath;

    public Task<ConsolidationState> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Read());
    }

    public async Task WriteAsync(ConsolidationState state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(_path)}.tmp");
        await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(ToHeartbeat(state), JsonOptions), cancellationToken);
        File.Move(tempPath, _path, true);
    }

    private ConsolidationState Read()
    {
        if (!File.Exists(_path))
        {
            return ConsolidationState.Unknown("consolidation state file does not exist yet");
        }

        string text;
        try
        {
            text = File.ReadAllText(_path);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return ConsolidationState.Unknown($"consolidation state file could not be read: {failure.Message}");
        }

        try
        {
            var heartbeat = JsonSerializer.Deserialize<Heartbeat>(text, JsonOptions);
            return heartbeat is { LastAttemptAt: not null }
                ? new ConsolidationState
                {
                    LastAttemptAt = heartbeat.LastAttemptAt,
                    LastSuccessAt = heartbeat.LastSuccessAt,
                    ConsolidatedClusters = heartbeat.ConsolidatedClusters,
                    LastError = heartbeat.LastError,
                }
                : ConsolidationState.Unknown("consolidation state file holds no recorded sweep attempts");
        }
        catch (JsonException failure)
        {
            return ConsolidationState.Unknown($"consolidation state file is not valid JSON: {failure.Message}");
        }
    }

    private static Heartbeat ToHeartbeat(ConsolidationState state) => new(
        state.LastAttemptAt,
        state.LastSuccessAt,
        state.ConsolidatedClusters,
        state.LastError);

    private sealed record Heartbeat(
        DateTimeOffset? LastAttemptAt,
        DateTimeOffset? LastSuccessAt,
        int? ConsolidatedClusters,
        string? LastError);
}
