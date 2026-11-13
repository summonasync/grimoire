using System.Text;
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
    public async Task Config_SetNullableNumberValue_WritesNumberNotString()
    {
        var code = await _host.RunAsync("config", "set", "sessions.retentionHours", "48");

        code.ShouldBe(ExitCodes.Ok);
        JsonNode.Parse(File.ReadAllText(_host.ConfigPath))!["sessions"]!["retentionHours"]!.GetValue<double>().ShouldBe(48);
        new ConfigLoader(_host.ConfigPath).Load().SessionsRetentionHoursOrDefault.ShouldBe(48);
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
    public async Task Config_SetTypeIncompatibleValue_RestoresPreviousFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_host.ConfigPath)!);
        const string original = "{\n  \"serve\": { \"port\": 5080 }\n}";
        File.WriteAllText(_host.ConfigPath, original);

        var code = await _host.RunAsync("config", "set", "serve.port", "2.5");

        code.ShouldBe(ExitCodes.Config);
        File.ReadAllText(_host.ConfigPath).ShouldBe(original);
    }

    [Fact]
    public async Task Config_SetTypeIncompatibleValueOnMissingFile_DeletesCreatedFile()
    {
        var code = await _host.RunAsync("config", "set", "serve.port", "2.5");

        code.ShouldBe(ExitCodes.Config);
        File.Exists(_host.ConfigPath).ShouldBeFalse();
    }

    [Fact]
    public async Task Config_SetSectionInsteadOfValue_Fails()
    {
        var code = await _host.RunAsync("config", "set", "qdrant", "oops");

        code.ShouldBe(ExitCodes.Config);
    }

    [Theory]
    [InlineData("sessions.retentionHours", "NaN")]
    [InlineData("sessions.retentionHours", "Infinity")]
    [InlineData("sessions.retentionHours", "-Infinity")]
    [InlineData("sessions.retentionHours", "1e999")]
    [InlineData("serve.port", "NaN")]
    [InlineData("serve.port", "Infinity")]
    [InlineData("serve.port", "-Infinity")]
    [InlineData("serve.port", "1e999")]
    public async Task Config_SetNonFiniteNumber_FailsWithConfigExit(string key, string value)
    {
        var original = SeedConfig();

        var code = await _host.RunAsync("config", "set", key, value);

        code.ShouldBe(ExitCodes.Config);
        _host.Output.ToString().ShouldContain($"Configuration property '{key}' must be a finite number, got '{value}'.");
        ConfigBytes().SequenceEqual(original).ShouldBeTrue();
    }

    [Theory]
    [InlineData("sessions.retentionHours")]
    [InlineData("serve.port")]
    public async Task Config_SetNonNumericText_KeepsPlainNumberMessageAndLeavesFileUntouched(string key)
    {
        var original = SeedConfig();

        var code = await _host.RunAsync("config", "set", key, "banana");

        code.ShouldBe(ExitCodes.Config);
        _host.Output.ToString().ShouldContain($"Configuration property '{key}' must be a number, got 'banana'.");
        _host.Output.ToString().ShouldNotContain("finite");
        ConfigBytes().SequenceEqual(original).ShouldBeTrue();
    }

    [Fact]
    public async Task Config_SetNonFiniteNumberOnMissingFile_LeavesNoConfigFile()
    {
        var code = await _host.RunAsync("config", "set", "sessions.retentionHours", "NaN");

        code.ShouldBe(ExitCodes.Config);
        File.Exists(_host.ConfigPath).ShouldBeFalse();
    }

    [Theory]
    [InlineData("serve.port", "6060", "\"port\": 6060")]
    [InlineData("sessions.retentionHours", "48", "\"retentionHours\": 48")]
    [InlineData("memory.dedupSimilarityThreshold", "0.85", "\"dedupSimilarityThreshold\": 0.85")]
    public async Task Config_SetFiniteNumber_StillWritesUnquotedNumber(string key, string value, string fragment)
    {
        SeedConfig();

        var code = await _host.RunAsync("config", "set", key, value);

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        File.ReadAllText(_host.ConfigPath).ShouldContain(fragment);
    }

    [Fact]
    public async Task Config_SetCollidingSessionCollection_RestoresPreviousFileByteForByte()
    {
        var original = SeedConfig();

        var code = await _host.RunAsync("config", "set", "qdrant.sessionCollection", "grimoire");

        code.ShouldBe(ExitCodes.Config);
        _host.Output.ToString().ShouldContain("error [config]: Qdrant collections are invalid:");
        ConfigBytes().SequenceEqual(original).ShouldBeTrue();
        new ConfigLoader(_host.ConfigPath).Load().Qdrant.SessionCollection.ShouldBeNull();
    }

    [Fact]
    public async Task Config_SetCollidingSessionCollectionOnMissingFile_DeletesCreatedFile()
    {
        var code = await _host.RunAsync("config", "set", "qdrant.sessionCollection", "grimoire");

        code.ShouldBe(ExitCodes.Config);
        File.Exists(_host.ConfigPath).ShouldBeFalse();
    }

    [Fact]
    public async Task Config_SetDistinctSessionCollection_WritesIt()
    {
        SeedConfig();

        var code = await _host.RunAsync("config", "set", "qdrant.sessionCollection", "grimoire-archive");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        new ConfigLoader(_host.ConfigPath).Load().Qdrant.SessionCollection.ShouldBe("grimoire-archive");
    }

    [Fact]
    public async Task Config_UnknownAction_ExitsUsage()
    {
        var code = await _host.RunAsync("config", "export");

        code.ShouldBe(ExitCodes.Usage);
    }

    public void Dispose() => _host.Dispose();

    private byte[] SeedConfig()
    {
        var bytes = Encoding.UTF8.GetBytes("""
            {
              "serve": { "port": 5080 },
              "sessions": { "retentionHours": 24 }
            }
            """);
        Directory.CreateDirectory(Path.GetDirectoryName(_host.ConfigPath)!);
        File.WriteAllBytes(_host.ConfigPath, bytes);
        return bytes;
    }

    private byte[] ConfigBytes() =>
        File.Exists(_host.ConfigPath) ? File.ReadAllBytes(_host.ConfigPath) : [];
}
