namespace Grimoire.Core;

public sealed record GcResult
{
    public required int ExpiredSessions { get; init; }

    public required int AgedTombstones { get; init; }

    public int TotalRemoved => ExpiredSessions + AgedTombstones;
}
