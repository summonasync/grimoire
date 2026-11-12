namespace Grimoire.Core;

public sealed record BriefDailyEntry
{
    public required Guid Id { get; init; }

    public required string Content { get; init; }

    public required bool Truncated { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
