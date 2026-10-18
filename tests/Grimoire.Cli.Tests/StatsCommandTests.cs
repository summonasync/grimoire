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

    public void Dispose() => _host.Dispose();
}
