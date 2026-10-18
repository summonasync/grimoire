using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

internal sealed class FakeEmbeddingProvider : IEmbeddingProvider
{
    private readonly Dictionary<string, ReadOnlyMemory<float>> _vectors = new(StringComparer.Ordinal);

    public List<string> Requests { get; } = [];

    public Exception? Failure { get; set; }

    public void Register(string text, params float[] vector) => _vectors[text] = vector;

    public Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        Requests.Add(text);

        if (Failure is not null)
        {
            throw Failure;
        }

        return Task.FromResult(_vectors.TryGetValue(text, out var vector) ? vector : HashVector(text));
    }

    internal static float[] HashVector(string text)
    {
        var vector = new float[16];
        for (var i = 0; i < text.Length; i++)
        {
            vector[i % vector.Length] += (text[i] % 32) - 16;
        }

        return vector;
    }
}
