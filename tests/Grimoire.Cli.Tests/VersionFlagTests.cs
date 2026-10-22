using System.Reflection;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class VersionFlagTests
{
    [Theory]
    [InlineData("--version")]
    [InlineData("--version", "stats")]
    [InlineData("--version", "--config", "/nonexistent/grimoire.json")]
    public async Task VersionFlag_WinsOverRemainingArgs_ExitsZero(params string[] args)
    {
        using var host = new TestHost();

        var code = await host.RunAsync(args);

        code.ShouldBe(ExitCodes.Ok);
        host.Output.ToString().TrimEnd('\r', '\n').ShouldBe($"grimoire {ProductVersion.Text}");
    }

    [Fact]
    public async Task VersionFlag_OutputIsOneMachineParsableLineAndTouchesNoStoreCalls()
    {
        using var host = new TestHost();

        var code = await host.RunAsync("--version");

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        var lines = host.Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.ShouldHaveSingleItem();
        lines[0].TrimEnd('\r').ShouldMatch(@"^grimoire \d+\.\d+\.\d+$");
        host.Store.Upserted.ShouldBeEmpty();
        host.Store.Searched.ShouldBeEmpty();
        host.Store.ListedFilters.ShouldBeEmpty();
    }

    [Fact]
    public async Task VersionFlag_SurvivesUnreadableConfig_BecauseItShortCircuitsBeforeConfigLoad()
    {
        var poison = new Dictionary<string, string> { ["GRIMOIRE_LOG"] = "debug" };
        using var statsHost = new TestHost(configEnvironment: poison);
        using var versionHost = new TestHost(configEnvironment: poison);

        var statsCode = await statsHost.RunAsync("stats");
        var versionCode = await versionHost.RunAsync("--version");

        statsCode.ShouldBe(ExitCodes.Config);
        versionCode.ShouldBe(ExitCodes.Ok);
        versionHost.Output.ToString().TrimEnd('\r', '\n').ShouldBe($"grimoire {ProductVersion.Text}");
    }

    [Fact]
    public void ProductVersion_IsThreePartNumericWithoutSourceRevisionMetadata()
    {
        ProductVersion.Text.ShouldMatch(@"^\d+\.\d+\.\d+$");
        ProductVersion.Text.ShouldNotContain("+");
    }

    [Fact]
    public void ProductVersion_StripsTheInformationalSourceBuildMetadata()
    {
        var informational = typeof(ProductVersion).Assembly
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

        if (informational.Contains('+'))
        {
            informational[..informational.IndexOf('+')].ShouldBe(ProductVersion.Text);
        }
        else
        {
            informational.ShouldBe(ProductVersion.Text);
        }
    }
}
