namespace Grimoire.Core;

public sealed record BriefTombstone
{
    public required Guid Id { get; init; }

    public required string? Reason { get; init; }

    public required DateTimeOffset TombstonedAt { get; init; }
}
