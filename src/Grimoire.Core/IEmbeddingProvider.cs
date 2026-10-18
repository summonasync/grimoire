namespace Grimoire.Core;

public interface IEmbeddingProvider
{
    Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken cancellationToken);
}
