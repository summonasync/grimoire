using Grimoire.Api;
using Grimoire.Cli.Commands;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class ServeStampWarningTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "grimoire-cli-tests", "serve-stamp", Guid.NewGuid().ToString("N"));

    public ServeStampWarningTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Fact]
    public void MatchingStamp_BootsSilently()
    {
        WriteStamp(SpaStampVerifier.BinaryVersion);

        var line = Warn();

        line.ShouldBe(string.Empty);
    }

    [Fact]
    public void StaleStamp_WarnsNamingBothVersions()
    {
        SpaStampVerifier.BinaryVersion.ShouldNotBe("0.1.0");
        WriteStamp("0.1.0");

        var line = Warn();

        line.ShouldContain("0.1.0");
        line.ShouldContain(SpaStampVerifier.BinaryVersion);
        line.ShouldNotContain("unknown");
        line.ShouldStartWith("Warning: ");
    }

    [Fact]
    public void AbsentStamp_WarnsUnknownAndNeverClaimsStale()
    {
        var line = Warn();

        line.ShouldContain("unknown");
        line.ShouldNotContain("stale");
        line.ShouldContain(SpaStampVerifier.BinaryVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("""{"productVersion": 42}""")]
    public void UnmeasurableStamp_WarnsUnknownWithoutThrowing(string content)
    {
        File.WriteAllText(Path.Combine(_root, SpaStampVerifier.StampFileName), content);

        var line = Warn();

        line.ShouldContain("unknown");
        line.ShouldNotContain("stale");
    }

    [Fact]
    public void Warning_IsASingleLine()
    {
        WriteStamp("0.1.0");

        Warn().ShouldNotContain("\n");
    }

    [Fact]
    public void StaticRootNotDeployed_WarnsUnknown()
    {
        var verdict = SpaStampVerifier.Read(Path.Combine(_root, "web-not-deployed"));

        verdict.State.ShouldBe(SpaStampState.Unknown);
        Warn(verdict).ShouldContain("unknown");
    }

    private string Warn(SpaStampVerdict? verdict = null)
    {
        using var host = new TestHost();
        ServeCommand.WriteStampWarning(host.Env.Output, verdict ?? SpaStampVerifier.Read(_root));
        var text = host.Env.Output.ToString() ?? string.Empty;
        return text.TrimEnd('\r', '\n');
    }

    private void WriteStamp(string productVersion) => File.WriteAllText(
        Path.Combine(_root, SpaStampVerifier.StampFileName),
        $$"""{"productVersion": "{{productVersion}}", "webSha": "a1b2c3d", "builtAt": "2026-11-12T10:00:00Z"}""");
}
