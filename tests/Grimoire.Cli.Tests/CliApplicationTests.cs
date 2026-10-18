using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class CliApplicationTests : IDisposable
{
    private readonly TestHost _host = new(new StubEmbeddings(new Dictionary<string, float[]>
    {
        ["deploy"] = [1f, 0f],
    }));

    [Fact]
    public async Task RunAsync_RoutesRememberAgainstFakes()
    {
        var code = await _host.RunAsync("remember", "deploy", "--scope", "session", "--session", "s1");

        code.ShouldBe(ExitCodes.Ok);
        _host.Store.Upserted.ShouldHaveSingleItem().Content.ShouldBe("deploy");
        _host.Output.ToString().ShouldContain("Remembered");
    }

    [Fact]
    public async Task RunAsync_RoutesRecallAgainstFakes()
    {
        _host.Store.Add("deploy on friday", [1f, 0f]);

        var code = await _host.RunAsync("recall", "deploy");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Embeddings.Requests.ShouldContain("deploy");
        _host.Output.ToString().ShouldContain("deploy on friday");
    }

    [Fact]
    public async Task RunAsync_RoutesListAgainstFakes()
    {
        var record = _host.Store.Add("deploy on friday", [1f, 0f]);

        var code = await _host.RunAsync("list");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain(record.Id.ToString());
    }

    [Fact]
    public async Task RunAsync_RoutesForgetByIdAgainstFakes()
    {
        var record = _host.Store.Add("deploy on friday", [1f, 0f]);

        var code = await _host.RunAsync("forget", record.Id.ToString(), "--reason", "stale");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Store.Records.ShouldContain(stored => stored.Id == record.Id && stored.Status == MemoryStatus.Tombstoned);
    }

    [Fact]
    public async Task RunAsync_RoutesGcAgainstFakes()
    {
        var code = await _host.RunAsync("gc");

        code.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain("expired sessions: 0, aged tombstones: 0");
    }

    [Fact]
    public async Task RunAsync_RoutesPersonaShowAgainstFakes()
    {
        var code = await _host.RunAsync("persona");

        code.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain("identity:");
    }

    [Fact]
    public async Task RunAsync_RoutesStatsAgainstFakes()
    {
        var code = await _host.RunAsync("stats");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("Pending compaction: 0");
    }

    [Fact]
    public async Task RunAsync_RoutesConfigPathAgainstFakes()
    {
        var code = await _host.RunAsync("config", "path");

        code.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().TrimEnd().ShouldBe(_host.ConfigPath);
    }

    [Fact]
    public async Task RunAsync_UnknownCommand_ExitsUsage()
    {
        var code = await _host.RunAsync("frobnicate");

        code.ShouldBe(ExitCodes.Usage);
        _host.Output.ToString().ShouldContain("error [usage]");
    }

    [Fact]
    public async Task RunAsync_NoArguments_PrintsHelpAndExitsZero()
    {
        var code = await _host.RunAsync();

        code.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain("Commands:");
    }

    [Fact]
    public async Task RunAsync_HelpTopic_PrintsCommandDetails()
    {
        var code = await _host.RunAsync("help", "remember");

        code.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain("--ttl");
    }

    [Fact]
    public async Task RunAsync_EmbeddingOffline_ExitsDependency()
    {
        _host.Embeddings.UnavailableText = "deploy";

        var code = await _host.RunAsync("recall", "deploy");

        code.ShouldBe(ExitCodes.Dependency);
        _host.Output.ToString().ShouldContain("error [dependency]");
    }

    public void Dispose() => _host.Dispose();
}
