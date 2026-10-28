namespace Grimoire.Core;

public sealed record ConsolidationState
{
    public DateTimeOffset? LastAttemptAt { get; init; }

    public DateTimeOffset? LastSuccessAt { get; init; }

    public int? ConsolidatedClusters { get; init; }

    public string? LastError { get; init; }

    public string? UnknownReason { get; init; }

    public bool Stale { get; init; }

    public bool IsUnknown => UnknownReason is not null;

    public static ConsolidationState Unknown(string reason) => new() { UnknownReason = reason };

    public bool IsStale(TimeSpan threshold, DateTimeOffset now) =>
        IsUnknown || LastSuccessAt is not { } success || now - success > threshold;
}
