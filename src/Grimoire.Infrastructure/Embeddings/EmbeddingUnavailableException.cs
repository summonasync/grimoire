namespace Grimoire.Infrastructure.Embeddings;

public sealed class EmbeddingUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
