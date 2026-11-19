using Grimoire.Core;

namespace Grimoire.Cli.Tests.Fakes;

public sealed class StubEmbeddings(IReadOnlyDictionary<string, float[]> vectors) : IEmbeddingProvider
{
    public List<string> Requests { get; } = [];

    public string? UnavailableText { get; set; }

    public Exception? EmbedFailure { get; set; }

    public Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        Requests.Add(text);

        if (EmbedFailure is { } failure)
        {
            throw failure;
        }

        if (text == UnavailableText)
        {
            throw new UpstreamUnavailableException("ollama", new HttpRequestException("connection refused"));
        }

        if (!vectors.TryGetValue(text, out var vector))
        {
            throw new KeyNotFoundException($"No embedding registered for '{text}'.");
        }

        return Task.FromResult(new ReadOnlyMemory<float>(vector));
    }
}
