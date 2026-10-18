namespace Grimoire.Core.Tests.Fakes;

public sealed class FakeEmbeddingProvider(IReadOnlyDictionary<string, float[]> vectors) : IEmbeddingProvider
{
    public List<string> Requests { get; } = [];

    public Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        Requests.Add(text);

        if (!vectors.TryGetValue(text, out var vector))
        {
            throw new KeyNotFoundException($"No embedding registered for '{text}'.");
        }

        return Task.FromResult(new ReadOnlyMemory<float>(vector));
    }
}
