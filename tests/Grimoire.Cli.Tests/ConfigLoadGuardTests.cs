using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class ConfigLoadGuardTests : IDisposable
{
    private readonly TestHost _host = new();

    [Fact]
    public async Task Stats_WithCollidingSessionCollection_RefusesBeforeAnyStoreIsReached()
    {
        WriteConfig("""{ "qdrant": { "collection": "grimoire", "sessionCollection": "grimoire" } }""");

        var code = await _host.RunAsync("stats");

        code.ShouldBe(ExitCodes.Config);
        var output = _host.Output.ToString();
        output.ShouldContain("error [config]: Qdrant collections are invalid:");
        output.ShouldNotContain("Active:");
    }

    [Fact]
    public async Task Stats_WithDistinctCollections_RunsTheCommandBody()
    {
        WriteConfig("""{ "qdrant": { "collection": "grimoire", "sessionCollection": "grimoire-sessions" } }""");

        var code = await _host.RunAsync("stats");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("Active: 0");
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private void WriteConfig(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_host.ConfigPath)!);
        File.WriteAllText(_host.ConfigPath, json);
    }
}
