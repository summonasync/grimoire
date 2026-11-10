using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class InstallCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "grimoire-cli-tests", Guid.NewGuid().ToString("N"));

    private readonly string _home;

    public InstallCommandTests()
    {
        _home = Path.Combine(_root, "home");
        Directory.CreateDirectory(_home);
    }

    [Fact]
    public async Task Install_MissingHomes_AreSkippedWithoutCreating()
    {
        using var host = NewHost();

        var code = await host.RunAsync("install");

        code.ShouldBe(ExitCodes.Ok);
        SkippedLines(host.Output.ToString()).ShouldBe(4);
        Directory.GetFiles(_home, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task Install_ExistingHomeFile_AppendsTheBlockAndSkipsTheRest()
    {
        using var host = NewHost();
        var claude = Path.Combine(_home, ".claude", "CLAUDE.md");
        Directory.CreateDirectory(Path.GetDirectoryName(claude)!);
        File.WriteAllText(claude, "intro\n");

        var code = await host.RunAsync("install");

        code.ShouldBe(ExitCodes.Ok);
        File.ReadAllText(claude).ShouldBe($"intro\n\n{InstallBlock.Text}\n");
        host.Output.ToString().ShouldContain($"{claude}: updated");
        SkippedLines(host.Output.ToString()).ShouldBe(3);
    }

    [Fact]
    public async Task Install_ReRunWithoutChanges_ReportsUnchanged()
    {
        using var host = NewHost();
        var claude = Path.Combine(_home, ".claude", "CLAUDE.md");
        Directory.CreateDirectory(Path.GetDirectoryName(claude)!);
        File.WriteAllText(claude, "intro\n");
        await host.RunAsync("install");

        using var rerun = NewHost();
        var code = await rerun.RunAsync("install");

        code.ShouldBe(ExitCodes.Ok);
        rerun.Output.ToString().ShouldContain($"{claude}: unchanged");
    }

    [Fact]
    public async Task Install_Remove_StripsTheBlockAndRestoresTheFile()
    {
        using var host = NewHost();
        var claude = Path.Combine(_home, ".claude", "CLAUDE.md");
        Directory.CreateDirectory(Path.GetDirectoryName(claude)!);
        File.WriteAllText(claude, "intro\n");
        await host.RunAsync("install");

        var code = await host.RunAsync("install", "--remove");

        code.ShouldBe(ExitCodes.Ok);
        File.ReadAllText(claude).ShouldBe("intro\n");
        host.Output.ToString().ShouldContain($"{claude}: removed");
    }

    [Fact]
    public async Task Install_PrintBlock_WritesTheBlockVerbatim()
    {
        using var host = NewHost();

        var code = await host.RunAsync("install", "--print-block");

        code.ShouldBe(ExitCodes.Ok);
        host.Output.ToString().ShouldBe(InstallBlock.Text + Environment.NewLine);
    }

    [Fact]
    public async Task Install_RemoveAndPrintBlock_AreMutuallyExclusive()
    {
        using var host = NewHost();

        var code = await host.RunAsync("install", "--remove", "--print-block");

        code.ShouldBe(ExitCodes.Usage);
        host.Output.ToString().ShouldContain("mutually exclusive");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private TestHost NewHost() => new(
        variables: new Dictionary<string, string> { ["GRIMOIRE_HOME"] = _home });

    private static int SkippedLines(string output) =>
        output.Split('\n').Count(line => line.EndsWith("skipped (not found)", StringComparison.Ordinal));
}
