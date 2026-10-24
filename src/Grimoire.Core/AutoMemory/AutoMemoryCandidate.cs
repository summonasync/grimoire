namespace Grimoire.Core;

public sealed record AutoMemoryCandidate
{
    public required string FileName { get; init; }

    public required string Content { get; init; }

    public required DateTimeOffset ModifiedAt { get; init; }
}
