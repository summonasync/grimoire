using Grimoire.Core;

namespace Grimoire.Infrastructure.Tests.Fakes;

public sealed class FakeEmbeddingProvider(IReadOnlyDictionary<string, float[]> vectors) : IEmbeddingProvider
{
    public List<string> Requests { get; } = [];

    public string? FailureText { get; set; }

    public Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        Requests.Add(text);

        if (text == FailureText)
        {
            throw new UpstreamUnavailableException("ollama", new HttpRequestException("connection refused"));
        }

        return Task.FromResult(new ReadOnlyMemory<float>(vectors[text]));
    }
}
