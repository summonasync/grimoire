namespace Grimoire.Infrastructure.Configuration;

/// <summary>
/// Session registry knobs. <see cref="RetentionHours"/> is deliberately nullable: unset
/// means "follow memory.sessionTtlHours", resolved once through
/// <see cref="GrimoireOptions.SessionsRetentionHoursOrDefault"/> rather than per consumer.
/// </summary>
public sealed record SessionsOptions
{
    public double? RetentionHours { get; init; }
}
