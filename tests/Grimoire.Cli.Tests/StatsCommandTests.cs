using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class StatsCommandTests : IDisposable
{
    private readonly TestHost _host = new();

    [Fact]
    public async Task Stats_ReportsCountsPerStatusAndScope()
    {
        _host.Store.Add("first project note", [1f, 0f], projectId: "grimoire");
        _host.Store.Add("second project note", [0f, 1f], projectId: "grimoire");
        _host.Store.Add(
            "buried",
            [1f, 1f],
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "x",
            tombstonedAt: _host.Clock.UtcNow);

        var code = await _host.RunAsync("stats");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("Active: 2 (session: 0, project: 2, global: 0, personal: 0)");
        output.ShouldContain("Superseded: 0");
        output.ShouldContain("Tombstoned: 1");
        output.ShouldContain("Idempotency conflicts: 0");
        output.ShouldContain("GC: unknown (gc state file does not exist yet), stale: true");
    }

    [Fact]
    public async Task Stats_RendersTheGcHeartbeatLine()
    {
        _host.GcState.Current = new GcState
        {
            LastAttemptAt = _host.Clock.UtcNow.AddMinutes(-10),
            LastSuccessAt = _host.Clock.UtcNow.AddMinutes(-10),
            RemovedExpired = 2,
            RemovedTombstones = 1,
        };

        var code = await _host.RunAsync("stats");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain(
            $"GC: last attempt {FormatMoment(_host.Clock.UtcNow.AddMinutes(-10))}, " +
            $"last success {FormatMoment(_host.Clock.UtcNow.AddMinutes(-10))}, " +
            "removed 2 expired / 1 tombstones, last error: none, stale: false");
    }

    [Fact]
    public async Task Stats_FlagsTheGcHeartbeatStalePastTwiceTheSweepInterval()
    {
        _host.GcState.Current = new GcState
        {
            LastAttemptAt = _host.Clock.UtcNow.AddHours(-5),
            LastSuccessAt = _host.Clock.UtcNow.AddHours(-5),
        };

        var code = await _host.RunAsync("stats");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("stale: true");
    }

    [Fact]
    public async Task Stats_CountsActiveIdempotencyDuplicates()
    {
        foreach (var content in new[] { "crashed write a", "crashed write b" })
        {
            _host.Store.Seed(
                new MemoryRecord
                {
                    Id = Guid.NewGuid(),
                    Content = content,
                    Scope = MemoryScope.Global,
                    Kind = MemoryKind.Journal,
                    Actor = MemoryActor.Human,
                    CreatedAt = _host.Clock.UtcNow,
                    LastAccessedAt = _host.Clock.UtcNow,
                    IdempotencyKey = "shared-key",
                },
                [1f, 0f]);
        }

        var code = await _host.RunAsync("stats");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("Idempotency conflicts: 1");
    }

    [Fact]
    public async Task Stats_RendersPerTomeCounts()
    {
        Seed("alpha note", "alpha");
        Seed("second alpha note", "alpha");
        Seed("beta note", "beta");
        Seed(
            "buried beta note",
            "beta",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "x",
            tombstonedAt: _host.Clock.UtcNow);

        var code = await _host.RunAsync("stats");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain(
            "Per tome: alpha (2 active, 0 tombstoned, lastWrite " +
            $"{FormatMoment(_host.Clock.UtcNow)}), beta (1 active, 1 tombstoned, lastWrite {FormatMoment(_host.Clock.UtcNow)})");
    }

    [Fact]
    public async Task Stats_RendersPerProjectCountsForTheResolvedTome()
    {
        Seed("grimoire note", "default", projectId: "grimoire");
        Seed("second grimoire note", "default", projectId: "grimoire");
        Seed("wayfarer note", "default", projectId: "wayfarer");
        Seed("untagged note", "default");
        Seed(
            "buried grimoire note",
            "default",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "x",
            tombstonedAt: _host.Clock.UtcNow,
            projectId: "grimoire");

        var code = await _host.RunAsync("stats");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("Per project: cross-project (1), grimoire (2), wayfarer (1)");
    }

    [Fact]
    public async Task Stats_WithoutProjectRecords_ShowsNonePerProject()
    {
        var code = await _host.RunAsync("stats");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("Per project: (none)");
    }

    [Fact]
    public async Task Stats_OmitsThePerTomeCompactionLineWithoutTheFlag()
    {
        SeedCompactablePair();

        var code = await _host.RunAsync("stats");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldNotContain("Per-tome compaction:");
    }

    [Fact]
    public async Task Stats_RendersPerTomeCompactionWhenOptedIn()
    {
        SeedCompactablePair();

        var code = await _host.RunAsync("stats", "--include-tome-compaction");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("Per-tome compaction: default (2)");
    }

    [Fact]
    public async Task Stats_RendersPerTomeLastWriteFromTheNewestRecord()
    {
        var older = _host.Clock.UtcNow.AddDays(-9);
        _host.Store.Seed(
            new MemoryRecord
            {
                Id = Guid.NewGuid(),
                Content = "old alpha note",
                Scope = MemoryScope.Project,
                Kind = MemoryKind.Journal,
                Actor = MemoryActor.Human,
                Tome = "alpha",
                CreatedAt = older,
                LastAccessedAt = older,
            },
            [1f, 0f]);
        Seed("fresh beta note", "beta");

        var code = await _host.RunAsync("stats");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain($"alpha (1 active, 0 tombstoned, lastWrite {FormatMoment(older)})");
        output.ShouldContain($"beta (1 active, 0 tombstoned, lastWrite {FormatMoment(_host.Clock.UtcNow)})");
    }

    [Fact]
    public async Task Stats_WithoutIdentity_ShowsTheDefaultTome()
    {
        var code = await _host.RunAsync("stats");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("Tome: default (default)");
    }

    [Fact]
    public async Task Stats_WithClaimFile_ShowsTheClaimResolvedTome()
    {
        using var host = new TestHost(
            claims: new FixedClaimProvider(new ActorClaim { Alias = "dan", Tome = "platform-work" }));

        var code = await host.RunAsync("stats");

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        host.Output.ToString().ShouldContain("Tome: platform-work (claim file)");
    }

    private static string FormatMoment(DateTimeOffset moment) => moment.ToString("u");

    private void Seed(
        string content,
        string tome,
        MemoryStatus status = MemoryStatus.Active,
        string? tombstoneReason = null,
        DateTimeOffset? tombstonedAt = null,
        string? projectId = null)
    {
        _host.Store.Seed(new MemoryRecord
        {
            Id = Guid.NewGuid(),
            Content = content,
            Scope = MemoryScope.Project,
            Kind = MemoryKind.Journal,
            Actor = MemoryActor.Human,
            Tome = tome,
            ProjectId = projectId,
            CreatedAt = _host.Clock.UtcNow,
            LastAccessedAt = _host.Clock.UtcNow,
            Status = status,
            TombstoneReason = tombstoneReason,
            TombstonedAt = tombstonedAt,
        }, [1f, 0f]);
    }

    private void SeedCompactablePair()
    {
        foreach (var content in new[] { "stale journal one", "stale journal two" })
        {
            var accessed = _host.Clock.UtcNow.AddDays(-10);
            _host.Store.Seed(
                new MemoryRecord
                {
                    Id = Guid.NewGuid(),
                    Content = content,
                    Scope = MemoryScope.Project,
                    Kind = MemoryKind.Journal,
                    Actor = MemoryActor.Human,
                    Tome = "default",
                    CreatedAt = accessed,
                    LastAccessedAt = accessed,
                },
                [1f, 0f]);
        }
    }

    public void Dispose() => _host.Dispose();
}
