namespace Grimoire.Infrastructure.Board;

public sealed class BoardResponseException(string message, Exception? innerException = null)
    : Exception(message, innerException);
