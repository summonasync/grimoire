using Grimoire.Cli.Commands;
using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class ServeCommandTests
{
    [Fact]
    public void BuildHostOptions_MapsPortAndGcIntervalFromConfig()
    {
        var config = new GrimoireOptions { Serve = new ServeOptions { Port = 5080, GcIntervalMinutes = 12.6 } };

        var options = ServeCommand.BuildHostOptions(config, null);

        options.Port.ShouldBe(5080);
        options.GcIntervalMinutes.ShouldBe(13);
    }

    [Fact]
    public void BuildHostOptions_ClampsGcIntervalToAtLeastOneMinute()
    {
        var config = new GrimoireOptions { Serve = new ServeOptions { GcIntervalMinutes = 0.2 } };

        var options = ServeCommand.BuildHostOptions(config, null);

        options.GcIntervalMinutes.ShouldBe(1);
    }

    [Fact]
    public void BuildHostOptions_DefaultsWebRootToGrimoireExportDirectory()
    {
        var options = ServeCommand.BuildHostOptions(new GrimoireOptions(), null);

        options.StaticRootPath.ShouldBe(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".grimoire",
            "web"));
    }

    [Fact]
    public void BuildHostOptions_FlagOverridesWebRoot()
    {
        var options = ServeCommand.BuildHostOptions(new GrimoireOptions(), "/srv/grimoire-ui");

        options.StaticRootPath.ShouldBe("/srv/grimoire-ui");
    }
}
