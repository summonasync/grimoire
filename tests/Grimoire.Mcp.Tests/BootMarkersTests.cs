namespace Grimoire.Mcp.Tests;

public sealed class BootMarkersTests
{
    [Fact]
    public void Codes_AreThePinnedClosedSet()
    {
        BootMarkers.Codes.ShouldBe([
            "unknown_env_key",
            "config_invalid",
            "dependency_unresolved",
            "transport_unavailable",
            "host_fault",
        ], ignoreOrder: true);
    }

    [Theory]
    [InlineData("unknown_env_key")]
    [InlineData("config_invalid")]
    [InlineData("dependency_unresolved")]
    [InlineData("transport_unavailable")]
    [InlineData("host_fault")]
    public void ReasonOf_RoundTripsEveryCode(string code)
    {
        BootMarkers.CodeOf(BootMarkers.ReasonOf(code)).ShouldBe(code);
    }

    [Fact]
    public void ReasonOf_UnknownCode_ThrowsWithClosedSetListed()
    {
        var ex = Should.Throw<ArgumentException>(() => BootMarkers.ReasonOf("mount_bleed"));
        ex.Message.ShouldContain("is not a mount_fail reason code");
        ex.Message.ShouldContain("unknown_env_key");
    }

    [Fact]
    public void MountOkLine_CarriesNameAndVersion()
    {
        BootMarkers.MountOkLine("grimoire", "0.2.0").ShouldBe("mount_ok grimoire/0.2.0");
    }

    [Fact]
    public void MountFailLine_IsOneGreppableLineWithReasonDetail()
    {
        var line = BootMarkers.MountFailLine(BootReason.UnknownEnvKey, "Environment override 'GRIMOIRE_LOG'\ndoes not match");

        line.ShouldStartWith("mount_fail:unknown_env_key ");
        line.ShouldNotContain('\n');
        line.ShouldContain("Environment override 'GRIMOIRE_LOG' does not match");
    }

    [Theory]
    [InlineData("Environment override 'GRIMOIRE_LOG' does not match any configuration property.")]
    [InlineData("Environment override 'GRIMOIRE_' carries no property path.")]
    [InlineData("Environment override 'GRIMOIRE_QDRANT__DEAD__ENDPOINT' targets 'dead' which is not a section.")]
    public void ClassifyConfigException_NonMatchingEnvOverrides_AreUnknownEnvKey(string message)
    {
        BootMarkers.ClassifyConfigException(message).ShouldBe(BootReason.UnknownEnvKey);
    }

    [Theory]
    [InlineData("Config file '/home/u/.grimoire/config.json' is not valid JSON: 'a' is invalid.")]
    [InlineData("Configuration is invalid: The JSON value could not be converted.")]
    [InlineData("Environment override 'GRIMOIRE_SERVE__PORT' must be a number for 'port'.")]
    public void ClassifyConfigException_EverythingElse_IsConfigInvalid(string message)
    {
        BootMarkers.ClassifyConfigException(message).ShouldBe(BootReason.ConfigInvalid);
    }

    [Fact]
    public void ClassifyHostException_MapsWiringTransportAndFallback()
    {
        BootMarkers.ClassifyHostException(new InvalidOperationException("Grimoire MCP host requires IMemoryStore in the service provider."))
            .ShouldBe(BootReason.DependencyUnresolved);
        BootMarkers.ClassifyHostException(new IOException("pipe closed"))
            .ShouldBe(BootReason.TransportUnavailable);
        BootMarkers.ClassifyHostException(new ObjectDisposedException("transport"))
            .ShouldBe(BootReason.TransportUnavailable);
        BootMarkers.ClassifyHostException(new Exception("wild"))
            .ShouldBe(BootReason.HostFault);
    }
}
