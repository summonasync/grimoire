using Grimoire.Cli.Commands;
using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class ServeRetentionTests
{
    [Theory]
    [InlineData(0d)]
    [InlineData(-3d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void BuildHostOptions_UnusableRetentionWindow_StopsServeBeforeAnySweepRuns(double hours)
    {
        var config = new GrimoireOptions { Sessions = new SessionsOptions { RetentionHours = hours } };

        var failure = Should.Throw<ConfigException>(() => ServeCommand.BuildHostOptions(config, null));

        failure.Message.ShouldContain("sessions.retentionHours");
        failure.Message.ShouldContain("positive finite");
    }

    [Fact]
    public void BuildHostOptions_GuardCoversTheSessionTtlFallback()
    {
        var config = new GrimoireOptions
        {
            Memory = new MemoryOptions { SessionTtlHours = 0 },
            Sessions = new SessionsOptions { RetentionHours = null },
        };

        Should.Throw<ConfigException>(() => ServeCommand.BuildHostOptions(config, null));
    }

    [Theory]
    [InlineData(6d)]
    [InlineData(0.25d)]
    [InlineData(8760d)]
    public void BuildHostOptions_UsableWindows_CarryTheConfiguredValue(double hours)
    {
        var config = new GrimoireOptions { Sessions = new SessionsOptions { RetentionHours = hours } };

        ServeCommand.BuildHostOptions(config, null).SessionsRetentionHours.ShouldBe(hours);
    }

    [Theory]
    [InlineData("""{ "sessions": { "retentionHours": 0 } }""")]
    [InlineData("""{ "sessions": { "retentionHours": -5 } }""")]
    [InlineData("""{ "memory": { "sessionTtlHours": 0 } }""")]
    public void RetentionFromAConfigFile_ReachesTheGuard_NotTheLoader(string json)
    {
        var path = WriteConfig(json);

        var loaded = new ConfigLoader(path, EmptyEnvironment).Load();
        var hours = loaded.SessionsRetentionHoursOrDefault;

        hours.ShouldBeLessThanOrEqualTo(0);
        Should.Throw<ConfigException>(() => ServeCommand.BuildHostOptions(loaded, null));
    }

    [Fact]
    public void RetentionFromAConfigFile_ANonNumericValue_DiesAtTheLoader()
    {
        var path = WriteConfig("""{ "sessions": { "retentionHours": "abc" } }""");

        Should.Throw<ConfigException>(() => new ConfigLoader(path, EmptyEnvironment).Load());
    }

    [Fact]
    public void RetentionFromTheEnvironment_ANonNumericOverride_FailsAtTheLoader()
    {
        var loader = new ConfigLoader(
            MissingPath(),
            new Dictionary<string, string> { ["GRIMOIRE_SESSIONS__RETENTIONHOURS"] = "not-a-number" });

        var failure = Should.Throw<ConfigException>(() => loader.Load());

        failure.Message.ShouldContain("must be a number");
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void RetentionFromTheEnvironment_NonFiniteLiterals_NeverBecomeOptions(string literal)
    {
        // The load door refuses the literal itself, so a non-finite window never reaches the
        // sweep's arithmetic and the operator gets the config class instead of a runtime one.
        var loader = new ConfigLoader(
            MissingPath(),
            new Dictionary<string, string> { ["GRIMOIRE_SESSIONS__RETENTIONHOURS"] = literal });

        var failure = Should.Throw<ConfigException>(() => loader.Load());

        failure.Message.ShouldContain("finite number");
        failure.Message.ShouldContain(literal);
    }

    [Fact]
    public async Task RunAsync_Serve_WithAZeroRetentionWindow_RefusesToStart()
    {
        using var host = new TestHost(configEnvironment:
            new Dictionary<string, string> { ["GRIMOIRE_SESSIONS__RETENTIONHOURS"] = "0" });

        var code = await CliApplication.RunAsync(
            ["serve", "--web-root", Path.GetTempPath()], host.Env, new CancellationToken(canceled: true));

        code.ShouldBe(ExitCodes.Config);
        var output = host.Output.ToString();
        output.ShouldContain("sessions.retentionHours");
        output.ShouldNotContain("Serving");
    }

    private static string MissingPath() => Path.Combine(Path.GetTempPath(), "grimoire-cli-tests", "absent-config.json");

    private static IReadOnlyDictionary<string, string> EmptyEnvironment => new Dictionary<string, string>();

    private static string WriteConfig(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), "grimoire-cli-tests", $"retention-{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
        return path;
    }
}
