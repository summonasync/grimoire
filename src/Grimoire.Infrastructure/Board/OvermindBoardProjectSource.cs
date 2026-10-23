using System.Net.Sockets;
using System.Text.Json;
using Grimoire.Core;

namespace Grimoire.Infrastructure.Board;

public sealed class OvermindBoardProjectSource(HttpClient http, string? portFilePath = null) : IBoardProjectSource
{
    public const string UpstreamName = "overmind";

    public const int FallbackPort = 4761;

    private static readonly JsonSerializerOptions Payload = new(JsonSerializerDefaults.Web);

    private readonly string _portFilePath = portFilePath
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".overmind",
            "host.port");

    public async Task<IReadOnlyList<BoardProject>> FetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await FetchCoreAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new UpstreamUnavailableException(UpstreamName, ex);
        }
        catch (SocketException ex)
        {
            throw new UpstreamUnavailableException(UpstreamName, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpstreamUnavailableException(UpstreamName, ex);
        }
        catch (JsonException ex)
        {
            throw new BoardResponseException($"Overmind projects payload is not valid JSON: {ex.Message}", ex);
        }
    }

    private async Task<IReadOnlyList<BoardProject>> FetchCoreAsync(CancellationToken cancellationToken)
    {
        var payload = await http.GetStringAsync($"{BaseAddress()}api/projects", cancellationToken);
        var entries = JsonSerializer.Deserialize<List<OvermindProject>>(payload, Payload)
            ?? throw new BoardResponseException("Overmind projects payload must be a JSON array.");

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Name))
            {
                throw new BoardResponseException("Overmind projects payload carries an entry without id or name.");
            }
        }

        return [.. entries.Select(entry => new BoardProject(
            entry.Id!,
            entry.Name!,
            TrimToNull(entry.Description),
            TrimToNull(entry.DiskPath)))];
    }

    private string BaseAddress()
    {
        var port = ReadPort();
        return $"http://127.0.0.1:{port}/";
    }

    private int ReadPort()
    {
        try
        {
            var text = File.ReadAllText(_portFilePath).Trim();
            return int.TryParse(text, out var port) && port is > 0 and < 65536 ? port : FallbackPort;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return FallbackPort;
        }
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record OvermindProject(string? Id, string? Name, string? Description, string? DiskPath);
}
