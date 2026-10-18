namespace Grimoire.Core;

public sealed record GcState
{
    public DateTimeOffset? LastAttemptAt { get; init; }

    public DateTimeOffset? LastSuccessAt { get; init; }

    public int? RemovedExpired { get; init; }

    public int? RemovedTombstones { get; init; }

    public string? LastError { get; init; }

    public string? UnknownReason { get; init; }

    public bool Stale { get; init; }

    public bool IsUnknown => UnknownReason is not null;

    public static GcState Unknown(string reason) => new() { UnknownReason = reason };

    public bool IsStale(TimeSpan threshold, DateTimeOffset now) =>
        IsUnknown || LastSuccessAt is not { } success || now - success > threshold;
}
