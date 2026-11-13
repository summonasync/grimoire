using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class GcCommandTests : IDisposable
{
    private readonly TestHost _host = new();

    [Fact]
    public async Task Gc_RemovesExpiredSessionsAndAgedTombstones()
    {
        _host.Store.Add(
            "expired session",
            [1f, 0f],
            scope: MemoryScope.Session,
            expiresAt: _host.Clock.UtcNow.AddHours(-1));
        _host.Store.Add(
            "aged tombstone",
            [0f, 1f],
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "old",
            tombstonedAt: _host.Clock.UtcNow.AddDays(-10));

        var code = await _host.RunAsync("gc");

        code.ShouldBe(ExitCodes.Ok);
        _host.Store.Records.ShouldBeEmpty();
        _host.Output.ToString().ShouldContain("expired sessions: 1, aged tombstones: 1");
    }

    [Fact]
    public async Task Gc_KeepsLiveRecordsAndYoungTombstones()
    {
        var live = _host.Store.Add("still valid", [1f, 0f]);
        var youngTombstone = _host.Store.Add(
            "recent tombstone",
            [0f, 1f],
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "fresh",
            tombstonedAt: _host.Clock.UtcNow.AddDays(-1));

        var code = await _host.RunAsync("gc");

        code.ShouldBe(ExitCodes.Ok);
        _host.Store.Records.ShouldContain(record => record.Id == live.Id);
        _host.Store.Records.ShouldContain(record => record.Id == youngTombstone.Id);
    }

    [Fact]
    public async Task Gc_ReportsSupersededExpiredSessionAsRetainedAndKeepsIt()
    {
        _host.Store.Add(
            "expired active session",
            [1f, 0f],
            scope: MemoryScope.Session,
            expiresAt: _host.Clock.UtcNow.AddHours(-1));
        var superseded = _host.Store.Add(
            "superseded session",
            [0f, 1f],
            scope: MemoryScope.Session,
            status: MemoryStatus.Superseded,
            supersededBy: Guid.NewGuid(),
            expiresAt: _host.Clock.UtcNow.AddHours(-2));

        var code = await _host.RunAsync("gc");

        code.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain(
            "expired sessions: 1, aged tombstones: 0, past TTL kept by policy: 1");
        _host.Store.Records.ShouldContain(record => record.Id == superseded.Id);
    }

    [Fact]
    public async Task ListAsync_WithStatusesList_ReturnsOnlyThoseStatuses()
    {
        var active = _host.Store.Add("active", [1f, 0f]);
        var superseded = _host.Store.Add(
            "superseded",
            [0f, 1f],
            status: MemoryStatus.Superseded,
            supersededBy: active.Id);

        var records = await _host.Store.ListAsync(
            new MemoryFilter { Statuses = [MemoryStatus.Superseded] },
            CancellationToken.None);

        records.Select(record => record.Id).ShouldBe([superseded.Id]);
    }

    public void Dispose() => _host.Dispose();
}
