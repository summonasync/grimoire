namespace Grimoire.Infrastructure.Memory;

public sealed class PayloadDecodeException(string message, Exception? innerException = null)
    : Exception(message, innerException);
