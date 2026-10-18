using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class ForgetCommandTests : IDisposable
{
    private readonly TestHost _host = new(new StubEmbeddings(new Dictionary<string, float[]>
    {
        ["where do we deploy"] = [1f, 0f],
        ["what about kubernetes"] = [0f, 1f],
    }));

    [Fact]
    public async Task Forget_ByIdTombstonesAnyStatusRecord()
    {
        var record = _host.Store.Add(
            "deploy on friday",
            [1f, 0f],
            status: MemoryStatus.Superseded,
            supersededBy: Guid.NewGuid());

        var code = await _host.RunAsync("forget", record.Id.ToString(), "--reason", "cleanup");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Store.Records.ShouldContain(stored =>
            stored.Id == record.Id &&
            stored.Status == MemoryStatus.Tombstoned &&
            stored.TombstoneReason == "cleanup");
        _host.Store.Deleted.ShouldBeEmpty();
    }

    [Fact]
    public async Task Forget_ByIdUnknownRecord_ExitsNotFound()
    {
        var code = await _host.RunAsync("forget", Guid.NewGuid().ToString());

        code.ShouldBe(ExitCodes.NotFound);
    }

    [Fact]
    public async Task Forget_ByIdAlreadyTombstoned_ExitsNotFound()
    {
        var record = _host.Store.Add(
            "deploy on friday",
            [1f, 0f],
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "first",
            tombstonedAt: FakeClock.DefaultStart);

        var code = await _host.RunAsync("forget", record.Id.ToString());

        code.ShouldBe(ExitCodes.NotFound);
    }

    [Fact]
    public async Task Forget_ByQuerySearchesExplicitlyActiveThenTombstonesBestMatch()
    {
        var best = _host.Store.Add("deploy on friday", [1f, 0f]);
        var weaker = _host.Store.Add("deploy sometimes", [0.6f, 0.8f]);

        var code = await _host.RunAsync("forget", "where do we deploy");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Store.Searched.ShouldHaveSingleItem().Filter.Status.ShouldBe(MemoryStatus.Active);
        _host.Store.Records.ShouldContain(stored => stored.Id == best.Id && stored.Status == MemoryStatus.Tombstoned);
        _host.Store.Records.ShouldContain(stored => stored.Id == weaker.Id && stored.Status == MemoryStatus.Active);
    }

    [Fact]
    public async Task Forget_ByQueryBelowThreshold_ExitsNotFound()
    {
        _host.Store.Add("deploy on friday", [1f, 0f]);

        var code = await _host.RunAsync("forget", "what about kubernetes");

        code.ShouldBe(ExitCodes.NotFound);
        _host.Store.Records.ShouldNotContain(stored => stored.Status == MemoryStatus.Tombstoned);
    }

    public void Dispose() => _host.Dispose();
}
