using System.Text.Json.Nodes;
using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class ConfigCommandTests : IDisposable
{
    private readonly TestHost _host = new();

    [Fact]
    public async Task Config_PathPrintsResolvedPath()
    {
        var code = await _host.RunAsync("config", "path");

        code.ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().TrimEnd().ShouldBe(_host.ConfigPath);
    }

    [Fact]
    public async Task Config_GetPrintsResolvedJson()
    {
        var code = await _host.RunAsync("config", "get");

        code.ShouldBe(ExitCodes.Ok);
        var root = JsonNode.Parse(_host.Output.ToString())!.AsObject();
        root.ContainsKey("qdrant").ShouldBeTrue();
        root.ContainsKey("memory").ShouldBeTrue();
    }

    [Fact]
    public async Task Config_GetReflectsEnvironmentOverrides()
    {
        using var host = new TestHost(configEnvironment: new Dictionary<string, string> { ["GRIMOIRE_SERVE__PORT"] = "6000" });

        var code = await host.RunAsync("config", "get");

        code.ShouldBe(ExitCodes.Ok);
        JsonNode.Parse(host.Output.ToString())!["serve"]!["port"]!.GetValue<int>().ShouldBe(6000);
    }

    [Fact]
    public async Task Config_SetWritesTypedValueAndCreatesFileWhenMissing()
    {
        var code = await _host.RunAsync("config", "set", "serve.port", "5080");

        code.ShouldBe(ExitCodes.Ok);
        var root = JsonNode.Parse(File.ReadAllText(_host.ConfigPath))!;
        root["serve"]!["port"]!.GetValue<int>().ShouldBe(5080);
        new ConfigLoader(_host.ConfigPath).Load().Serve.Port.ShouldBe(5080);
        _host.Output.ToString().ShouldContain("Set serve.port = 5080");
    }

    [Fact]
    public async Task Config_SetAcceptsNestedFractionValues()
    {
        var code = await _host.RunAsync("config", "set", "memory.dedupSimilarityThreshold", "0.8");

        code.ShouldBe(ExitCodes.Ok);
        new ConfigLoader(_host.ConfigPath).Load().Memory.DedupSimilarityThreshold.ShouldBe(0.8);
    }

    [Fact]
    public async Task Config_SetUnknownKey_FailsWithoutTouchingFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_host.ConfigPath)!);
        const string original = "{\n  \"serve\": { \"port\": 5080 }\n}";
        File.WriteAllText(_host.ConfigPath, original);

        var code = await _host.RunAsync("config", "set", "serv.port", "1");

        code.ShouldBe(ExitCodes.Config);
        File.ReadAllText(_host.ConfigPath).ShouldBe(original);
    }

    [Fact]
    public async Task Config_SetSectionInsteadOfValue_Fails()
    {
        var code = await _host.RunAsync("config", "set", "qdrant", "oops");

        code.ShouldBe(ExitCodes.Config);
    }

    [Fact]
    public async Task Config_UnknownAction_ExitsUsage()
    {
        var code = await _host.RunAsync("config", "export");

        code.ShouldBe(ExitCodes.Usage);
    }

    public void Dispose() => _host.Dispose();
}
