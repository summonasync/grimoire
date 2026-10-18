using Grimoire.Core;

namespace Grimoire.Api.Tests.Fakes;

public sealed class FakeEmbeddingProvider : IEmbeddingProvider
{
    public Dictionary<string, float[]> Vectors { get; } = [];

    public List<string> Requests { get; } = [];

    public Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        Requests.Add(text);

        if (!Vectors.TryGetValue(text, out var vector))
        {
            throw new KeyNotFoundException($"No embedding registered for '{text}'.");
        }

        return Task.FromResult(new ReadOnlyMemory<float>(vector));
    }
}
