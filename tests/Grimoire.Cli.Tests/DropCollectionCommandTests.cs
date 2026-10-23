using Grimoire.Cli.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class DropCollectionCommandTests : IDisposable
{
    private readonly RecordingVectorDb _db = new() { CollectionExists = true };

    private readonly TestHost _host;

    public DropCollectionCommandTests()
    {
        _host = new TestHost(db: _db);
    }

    [Fact]
    public async Task RunAsync_WithoutYes_RefusesWithUsageHintAndLeavesTheCollectionIntact()
    {
        var code = await _host.RunAsync("drop-collection");

        code.ShouldBe(ExitCodes.Usage);
        var output = _host.Output.ToString();
        output.ShouldContain("error [usage]");
        output.ShouldContain("--yes");
        _db.DroppedCount.ShouldBe(0);
        _db.CollectionExists.ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_WithYes_DropsTheCollectionAndReportsOk()
    {
        var code = await _host.RunAsync("drop-collection", "--yes");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _db.DroppedCount.ShouldBe(1);
        _db.CollectionExists.ShouldBeFalse();
        _host.Output.ToString().ShouldContain("Dropped collection 'grimoire'");
    }

    [Fact]
    public async Task RunAsync_WithYes_WhenCollectionMissing_ReportsNothingToDrop()
    {
        _db.CollectionExists = false;

        var code = await _host.RunAsync("drop-collection", "--yes");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _db.DroppedCount.ShouldBe(0);
        _host.Output.ToString().ShouldContain("does not exist, nothing to drop");
    }

    public void Dispose() => _host.Dispose();
}
