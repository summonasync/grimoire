using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;

namespace Grimoire.Infrastructure.Embeddings;

public sealed class OllamaEmbeddingProvider(HttpClient http, EmbeddingOptions options) : IEmbeddingProvider
{
    private const string UpstreamName = "ollama";

    private static readonly JsonSerializerOptions RequestOptions = new(JsonSerializerDefaults.Web);

    public async Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            return await EmbedCoreAsync(text, cancellationToken);
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
            throw new EmbeddingResponseException($"Ollama response for model '{options.Model}' is not valid JSON: {ex.Message}", ex);
        }
    }

    private async Task<ReadOnlyMemory<float>> EmbedCoreAsync(string text, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.Endpoint.TrimEnd('/')}/api/embeddings")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new OllamaEmbeddingRequest(options.Model, text), RequestOptions),
                Encoding.UTF8,
                "application/json"),
        };

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new EmbeddingResponseException($"Ollama returned HTTP {(int)response.StatusCode} for model '{options.Model}'.");
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        if (!document.RootElement.TryGetProperty("embedding", out var embedding)
            || embedding.ValueKind != JsonValueKind.Array)
        {
            throw new EmbeddingResponseException($"Ollama response for model '{options.Model}' carries no embedding array.");
        }

        var values = new List<float>();

        foreach (var item in embedding.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number)
            {
                throw new EmbeddingResponseException($"Ollama response for model '{options.Model}' carries a non-numeric embedding entry.");
            }

            values.Add(item.GetSingle());
        }

        if (values.Count == 0)
        {
            throw new EmbeddingResponseException($"Ollama response for model '{options.Model}' carries an empty embedding array.");
        }

        if (values.Count != options.Dimensions)
        {
            throw new EmbeddingResponseException(
                $"Ollama model '{options.Model}' returned {values.Count} dimensions but {options.Dimensions} are configured.");
        }

        return values.ToArray();
    }

    private sealed record OllamaEmbeddingRequest(string Model, string Prompt);
}
