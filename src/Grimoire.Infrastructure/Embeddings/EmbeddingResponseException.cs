namespace Grimoire.Infrastructure.Embeddings;

public sealed class EmbeddingResponseException(string message, Exception? innerException = null)
    : Exception(message, innerException);
