using System.Net;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class SpaStampTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "grimoire-api-tests", "spa-stamp", Guid.NewGuid().ToString("N"));

    public SpaStampTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Fact]
    public void BinaryVersion_ResolvesTheStampedProductVersion()
    {
        SpaStampVerifier.BinaryVersion.ShouldNotBe("0.0.0");
        Version.TryParse(SpaStampVerifier.BinaryVersion, out _).ShouldBeTrue();
    }

    [Fact]
    public void Read_StampMatchingTheBinary_ReturnsMatchWithoutWarning()
    {
        WriteStamp(SpaStampVerifier.BinaryVersion);

        var verdict = SpaStampVerifier.Read(_root);

        verdict.State.ShouldBe(SpaStampState.Match);
        verdict.ExportVersion.ShouldBe(SpaStampVerifier.BinaryVersion);
        verdict.WarningLine.ShouldBeNull();
    }

    [Fact]
    public void Read_StampWithBuildMetadata_ReturnsMatch()
    {
        WriteStamp($"{SpaStampVerifier.BinaryVersion}+a1b2c3d");

        SpaStampVerifier.Read(_root).State.ShouldBe(SpaStampState.Match);
    }

    [Fact]
    public void Read_StampOfADifferentVersion_ReturnsMismatchCarryingBothVersions()
    {
        SpaStampVerifier.BinaryVersion.ShouldNotBe("0.1.0");
        WriteStamp("0.1.0");

        var verdict = SpaStampVerifier.Read(_root);

        verdict.State.ShouldBe(SpaStampState.Mismatch);
        verdict.ExportVersion.ShouldBe("0.1.0");
        verdict.BinaryVersion.ShouldBe(SpaStampVerifier.BinaryVersion);
        var warning = verdict.WarningLine;
        warning.ShouldNotBeNullOrEmpty();
        warning!.ShouldContain("0.1.0");
        warning.ShouldContain(SpaStampVerifier.BinaryVersion);
        warning.ShouldNotContain("unknown");
    }

    [Fact]
    public void Read_NoStampAtAll_ReturnsUnknownAndNeverClaimsStale()
    {
        var verdict = SpaStampVerifier.Read(_root);

        verdict.State.ShouldBe(SpaStampState.Unknown);
        verdict.Cause.ShouldBe(SpaStampUnknownCause.Missing);
        verdict.ExportVersion.ShouldBeNull();
        var warning = verdict.WarningLine;
        warning.ShouldNotBeNullOrEmpty();
        warning!.ShouldContain("unknown");
        warning.ShouldNotContain("stale");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\t ")]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"webSha": "a1b2c3d"}""")]
    [InlineData("""{"productVersion": 42}""")]
    [InlineData("""{"productVersion": null}""")]
    [InlineData("""{"productVersion": "   "}""")]
    public void Read_StampThatCannotBeMeasured_ReturnsUnknownWithoutThrowing(string content)
    {
        File.WriteAllText(Path.Combine(_root, SpaStampVerifier.StampFileName), content);

        var verdict = SpaStampVerifier.Read(_root);

        verdict.State.ShouldBe(SpaStampState.Unknown);
        verdict.Cause.ShouldBeOneOf(SpaStampUnknownCause.Empty, SpaStampUnknownCause.Invalid);
        var warning = verdict.WarningLine;
        warning.ShouldNotBeNullOrEmpty();
        warning!.ShouldContain("unknown");
        warning.ShouldNotContain("stale");
    }

    [Fact]
    public void Read_StampPathIsADirectory_ReturnsUnknownWithoutThrowing()
    {
        Directory.CreateDirectory(Path.Combine(_root, SpaStampVerifier.StampFileName));

        SpaStampVerifier.Read(_root).State.ShouldBe(SpaStampState.Unknown);
    }

    [Fact]
    public void Read_ExtraStampKeys_AreIgnored()
    {
        File.WriteAllText(Path.Combine(_root, SpaStampVerifier.StampFileName),
            $$"""{"future": {"nested": true}, "productVersion": "{{SpaStampVerifier.BinaryVersion}}", "webSha": "a1b2c3d", "builtAt": "2026-11-12T10:00:00Z"}""");

        SpaStampVerifier.Read(_root).State.ShouldBe(SpaStampState.Match);
    }

    [Fact]
    public async Task Boot_UnreadableStamp_ServesTheAbsentExportFallbackUnchanged()
    {
        File.WriteAllText(Path.Combine(_root, SpaStampVerifier.StampFileName), "{ broken");
        using var factory = GrimoireApiFactory.Create(staticRootPath: _root);

        var response = await factory.NewClient().GetAsync("/memories");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Boot_MatchingStamp_ServesTheShell()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "index.html"), "<html><title>stamped spa</title></html>");
        WriteStamp(SpaStampVerifier.BinaryVersion);
        using var factory = GrimoireApiFactory.Create(staticRootPath: _root);

        var response = await factory.NewClient().GetAsync("/memories");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("stamped spa");
    }

    [Fact]
    public void Read_StaticRootNotDeployed_ReturnsUnknownWithoutThrowing()
    {
        var verdict = SpaStampVerifier.Read(Path.Combine(_root, "web-not-deployed"));

        verdict.State.ShouldBe(SpaStampState.Unknown);
        verdict.Cause.ShouldBe(SpaStampUnknownCause.Missing);
    }

    private void WriteStamp(string productVersion) => File.WriteAllText(
        Path.Combine(_root, SpaStampVerifier.StampFileName),
        $$"""{"productVersion": "{{productVersion}}", "webSha": "a1b2c3d", "builtAt": "2026-11-12T10:00:00Z"}""");
}
