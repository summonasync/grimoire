using Grimoire.Cli.Commands;
using Grimoire.Core;
using Grimoire.Mcp;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class McpCommandTests
{
    [Fact]
    public void BuildHostOptions_CarriesExplicitActor()
    {
        var options = McpCommand.BuildHostOptions(new MemoryActor("junior", "claude"));

        options.Actor.ShouldNotBeNull();
        options.Actor!.Value.Alias.ShouldBe("junior");
        options.Actor.Value.Vendor.ShouldBe("claude");
    }

    [Fact]
    public void BuildHostOptions_WithoutActorKeepsClientInfoFallback()
    {
        var options = McpCommand.BuildHostOptions(null);

        options.Actor.ShouldBeNull();
        options.ServerName.ShouldBe("grimoire");
        options.Instructions.ShouldBe(McpHostOptions.DefaultInstructions);
    }

    [Fact]
    public void BuildHostOptions_UsesAssemblyVersion()
    {
        var expected = typeof(McpCommand).Assembly.GetName().Version!.ToString(3);

        var options = McpCommand.BuildHostOptions(null);

        options.ServerVersion.ShouldBe(expected);
    }

    [Fact]
    public void BuildHostOptions_CarriesTheEnvCandidatesForTomeResolution()
    {
        var options = McpCommand.BuildHostOptions(null, "team-platform", "dan+claude");

        options.EnvTome.ShouldBe("team-platform");
        options.EnvActor.ShouldBe("dan+claude");
    }

    [Fact]
    public void BuildHostOptions_AnEnvOnlyActor_StaysBelowTheSessionChannel()
    {
        var options = McpCommand.BuildHostOptions(
            ActorResolver.Parse(null),
            "team-platform",
            "agent+claude");

        options.Actor.ShouldBeNull();
        options.EnvActor.ShouldBe("agent+claude");
    }

    [Fact]
    public void BuildHostOptions_TheExplicitFlag_ParsesIntoTheConfiguredActor()
    {
        var options = McpCommand.BuildHostOptions(
            ActorResolver.Parse("junior+claude"),
            null,
            "agent+claude");

        options.Actor.ShouldNotBeNull();
        options.Actor!.Value.Alias.ShouldBe("junior");
        options.Actor.Value.Vendor.ShouldBe("claude");
    }

    [Fact]
    public void DefaultInstructions_TeachBriefFirstStartupAndTheScryDoor()
    {
        var instructions = McpHostOptions.DefaultInstructions;

        instructions.ShouldContain("memory_brief");
        instructions.ShouldContain("memory_scry");
        instructions.IndexOf("memory_brief", StringComparison.Ordinal)
            .ShouldBeLessThan(instructions.IndexOf("memory_recall", StringComparison.Ordinal));
    }
}
