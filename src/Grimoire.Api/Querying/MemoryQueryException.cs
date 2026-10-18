namespace Grimoire.Api.Querying;

public sealed class MemoryQueryException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
