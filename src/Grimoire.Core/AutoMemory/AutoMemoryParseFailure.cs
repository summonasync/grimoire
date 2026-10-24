namespace Grimoire.Core;

public sealed record AutoMemoryParseFailure
{
    public required string FileName { get; init; }

    public required string Reason { get; init; }
}
