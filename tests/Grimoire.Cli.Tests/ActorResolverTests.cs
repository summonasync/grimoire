using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class ActorResolverTests
{
    private static readonly ActorClaim Claim = new() { Alias = "dan", Tome = "platform-work" };

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
    public void Parse_Unset_ReturnsNull()
    {
        ActorResolver.Parse(null).ShouldBeNull();
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

    [Fact]
    public async Task ResolveAsync_EnvActorImpliesTomeAndActor()
    {
        var identity = await ActorResolver.ResolveAsync(
            Environment(new Dictionary<string, string> { [ActorResolver.EnvironmentVariable] = "dan+claude" }),
            null, null, "default", new FixedClaimProvider(null), null, CancellationToken.None);

        identity.Tome.ShouldBe("dan");
        identity.Actor.ShouldBe(new MemoryActor("dan", "claude"));
        identity.Source.ShouldBe(TomeSource.EnvActor);
    }

    [Fact]
    public async Task ResolveAsync_EnvTomeWinsOverEnvActorTome()
    {
        var identity = await ActorResolver.ResolveAsync(
            Environment(new Dictionary<string, string>
            {
                ["GRIMOIRE_TOME"] = "platform-work",
                [ActorResolver.EnvironmentVariable] = "dan+claude",
            }),
            null, null, "default", new FixedClaimProvider(null), null, CancellationToken.None);

        identity.Tome.ShouldBe("platform-work");
        identity.Source.ShouldBe(TomeSource.EnvTome);
        identity.Actor.ShouldBe(new MemoryActor("dan", "claude"));
    }

    [Fact]
    public async Task ResolveAsync_ClaimBindsActorAliasToTome()
    {
        var identity = await ActorResolver.ResolveAsync(
            Environment(), null, null, "default", new FixedClaimProvider(Claim), null, CancellationToken.None);

        identity.Tome.ShouldBe("platform-work");
        identity.Source.ShouldBe(TomeSource.ClaimFile);
        identity.Actor.ShouldBe(new MemoryActor("dan", null));
        identity.Actor.IsHuman.ShouldBeFalse();
    }

    [Fact]
    public async Task ResolveAsync_ClaimAliasAloneImpliesTome()
    {
        var identity = await ActorResolver.ResolveAsync(
            Environment(), null, null, "default",
            new FixedClaimProvider(new ActorClaim { Alias = "dan" }), null, CancellationToken.None);

        identity.Tome.ShouldBe("dan");
        identity.Source.ShouldBe(TomeSource.ClaimFile);
        identity.Actor.Alias.ShouldBe("dan");
    }

    [Fact]
    public async Task ResolveAsync_ExplicitTomeWinsAndSkipsTheClaimWalk()
    {
        var claims = new FixedClaimProvider(Claim);

        var identity = await ActorResolver.ResolveAsync(
            Environment(), null, "research", "default", claims, null, CancellationToken.None);

        identity.Tome.ShouldBe("research");
        identity.Source.ShouldBe(TomeSource.ExplicitParam);
        identity.Actor.ShouldBe(MemoryActor.Human);
        claims.Reads.ShouldBe(0);
    }

    [Fact]
    public async Task ResolveAsync_ExplicitActorOverridesClaimAlias()
    {
        var identity = await ActorResolver.ResolveAsync(
            Environment(), "eve+anthropic", null, "default", new FixedClaimProvider(Claim), null, CancellationToken.None);

        identity.Tome.ShouldBe("platform-work");
        identity.Actor.ShouldBe(new MemoryActor("eve", "anthropic"));
    }

    [Fact]
    public async Task ResolveAsync_UnsetFallsBackToDefaultTomeAndHuman()
    {
        var identity = await ActorResolver.ResolveAsync(
            Environment(), null, null, "team", new FixedClaimProvider(null), null, CancellationToken.None);

        identity.Tome.ShouldBe("team");
        identity.Source.ShouldBe(TomeSource.Default);
        identity.Actor.ShouldBe(MemoryActor.Human);
    }

    [Theory]
    [InlineData("Platform Work", "platform-work")]
    [InlineData("PLATFORM", "platform")]
    public async Task ResolveAsync_NormalizesTomeCandidates(string candidate, string expected)
    {
        var identity = await ActorResolver.ResolveAsync(
            Environment(), null, candidate, "default", new FixedClaimProvider(null), null, CancellationToken.None);

        identity.Tome.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Not Valid!", "--tome")]
    [InlineData("", "--tome")]
    public async Task ResolveAsync_InvalidExplicitTome_ThrowsUsage(string candidate, string origin)
    {
        var failure = await Should.ThrowAsync<UsageException>(() =>
            ActorResolver.ResolveAsync(Environment(), null, candidate, "default", new FixedClaimProvider(null), null, CancellationToken.None));

        failure.Message.ShouldContain(origin);
    }

    [Fact]
    public async Task ResolveAsync_InvalidClaimTome_ThrowsUsageNamingTheClaimFile()
    {
        var failure = await Should.ThrowAsync<UsageException>(() =>
            ActorResolver.ResolveAsync(
                Environment(), null, null, "default",
                new FixedClaimProvider(new ActorClaim { Alias = "dan", Tome = "Not Valid!" }),
                null,
                CancellationToken.None));

        failure.Message.ShouldContain("AGENTS.md");
    }

    [Fact]
    public async Task ResolveAsync_InvalidEnvActorAlias_ThrowsUsage()
    {
        await Should.ThrowAsync<UsageException>(() =>
            ActorResolver.ResolveAsync(
                Environment(new Dictionary<string, string> { [ActorResolver.EnvironmentVariable] = "+claude" }),
                null, null, "default", new FixedClaimProvider(null), null, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_TheFlagBeatsTheIdentityFileAndTheEnvironment()
    {
        var identity = await ActorResolver.ResolveAsync(
            Environment(new Dictionary<string, string> { [ActorResolver.EnvironmentVariable] = "codex+openai" }),
            "junior+claude", null, "default", new FixedClaimProvider(null),
            new FixedIdentitySource(new MemoryActor("file-alias", "file-vendor")), CancellationToken.None);

        identity.Actor.ShouldBe(new MemoryActor("junior", "claude"));
    }

    [Fact]
    public async Task ResolveAsync_TheIdentityFileBeatsTheEnvironmentVariable()
    {
        var identity = await ActorResolver.ResolveAsync(
            Environment(new Dictionary<string, string> { [ActorResolver.EnvironmentVariable] = "codex+openai" }),
            null, null, "default", new FixedClaimProvider(null),
            new FixedIdentitySource(new MemoryActor("file-alias", "file-vendor")), CancellationToken.None);

        identity.Actor.ShouldBe(new MemoryActor("file-alias", "file-vendor"));
    }

    [Fact]
    public async Task ResolveAsync_AnAbsentIdentityFile_FallsThroughToTheEnvironment()
    {
        var identity = await ActorResolver.ResolveAsync(
            Environment(new Dictionary<string, string> { [ActorResolver.EnvironmentVariable] = "codex+openai" }),
            null, null, "default", new FixedClaimProvider(null),
            new FixedIdentitySource(null), CancellationToken.None);

        identity.Actor.ShouldBe(new MemoryActor("codex", "openai"));
    }

    [Fact]
    public async Task ResolveAsync_TheClaimVendorReachesTheActor()
    {
        var identity = await ActorResolver.ResolveAsync(
            Environment(), null, null, "default",
            new FixedClaimProvider(new ActorClaim { Alias = "dan", Vendor = "claude" }), null,
            CancellationToken.None);

        identity.Actor.ShouldBe(new MemoryActor("dan", "claude"));
    }
}
