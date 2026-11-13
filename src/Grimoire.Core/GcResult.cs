namespace Grimoire.Core;

public sealed record GcResult
{
    public required int ExpiredSessions { get; init; }

    public required int AgedTombstones { get; init; }

    // Deliberately outside TotalRemoved: these are rows GC left in place on purpose (session rows
    // past their TTL whose status keeps them under retention or under the correction chain).
    public int RetainedPastTtl { get; init; }

    public int TotalRemoved => ExpiredSessions + AgedTombstones;
}
