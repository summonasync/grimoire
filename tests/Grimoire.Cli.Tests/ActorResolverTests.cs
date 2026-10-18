using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class ActorResolverTests
{
    private static CliEnvironment Environment(IReadOnlyDictionary<string, string>? variables = null) => new()
    {
        ConfigPath = "config.json",
        Config = new ConfigLoader("config.json"),
        Variables = variables,
    };

    [Fact]
    public void Resolve_FlagWinsOverEnvironmentVariable()
    {
        var actor = ActorResolver.Resolve(
            Environment(new Dictionary<string, string> { [ActorResolver.EnvironmentVariable] = "codex+openai" }),
            "junior+claude");

        actor.Alias.ShouldBe("junior");
        actor.Vendor.ShouldBe("claude");
    }

    [Fact]
    public void Resolve_EnvironmentVariableProvidesAliasAndVendor()
    {
        var actor = ActorResolver.Resolve(
            Environment(new Dictionary<string, string> { [ActorResolver.EnvironmentVariable] = "codex+openai" }),
            null);

        actor.Alias.ShouldBe("codex");
        actor.Vendor.ShouldBe("openai");
        actor.IsHuman.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_UnsetDefaultsToHuman()
    {
        var actor = ActorResolver.Resolve(Environment(), null);

        actor.ShouldBe(MemoryActor.Human);
        actor.IsHuman.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_PlainAliasCarriesNoVendor()
    {
        var actor = ActorResolver.Resolve(Environment(), "junior");

        actor.Alias.ShouldBe("junior");
        actor.Vendor.ShouldBeNull();
    }

    [Fact]
    public void TryResolve_Unset_ReturnsNull()
    {
        ActorResolver.TryResolve(Environment(), null).ShouldBeNull();
    }

    [Theory]
    [InlineData("+claude")]
    [InlineData("+")]
    public void Resolve_MissingAlias_ThrowsUsage(string text)
    {
        Should.Throw<UsageException>(() => ActorResolver.Resolve(Environment(), text));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Resolve_BlankFlag_FallsBackToHuman(string text)
    {
        ActorResolver.Resolve(Environment(), text).ShouldBe(MemoryActor.Human);
    }
}
