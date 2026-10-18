using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class TomeResolverTests
{
    private readonly TomeResolver _resolver = new();

    [Fact]
    public void Resolve_ExplicitParam_WinsOverEveryLaterStep()
    {
        var resolution = _resolver.Resolve(
            explicitTome: "my-tome",
            envTome: "env-tome",
            envActor: "env-actor",
            claim: new ActorClaim { Alias = "claim-actor", Tome = "claim-tome" },
            clientActor: "client-actor");

        resolution.ResolvedTome.ShouldBe("my-tome");
        resolution.Source.ShouldBe(TomeSource.ExplicitParam);
    }

    [Fact]
    public void Resolve_EnvTome_WinsOverActorClaimAndClient()
    {
        var resolution = _resolver.Resolve(
            envTome: "env-tome",
            envActor: "env-actor",
            claim: new ActorClaim { Tome = "claim-tome" },
            clientActor: "client-actor");

        resolution.ResolvedTome.ShouldBe("env-tome");
        resolution.Source.ShouldBe(TomeSource.EnvTome);
    }

    [Fact]
    public void Resolve_EnvActor_WinsOverClaimAndClient()
    {
        var resolution = _resolver.Resolve(
            envActor: "env-actor",
            claim: new ActorClaim { Tome = "claim-tome" },
            clientActor: "client-actor");

        resolution.ResolvedTome.ShouldBe("env-actor");
        resolution.Source.ShouldBe(TomeSource.EnvActor);
    }

    [Fact]
    public void Resolve_ClaimTome_WinsOverClient()
    {
        var resolution = _resolver.Resolve(
            claim: new ActorClaim { Alias = "claim-actor", Tome = "claim-tome" },
            clientActor: "client-actor");

        resolution.ResolvedTome.ShouldBe("claim-tome");
        resolution.Source.ShouldBe(TomeSource.ClaimFile);
    }

    [Fact]
    public void Resolve_ClaimAliasImpliesTomeWhenTomeMissing()
    {
        var resolution = _resolver.Resolve(
            claim: new ActorClaim { Alias = "Claim Actor" },
            clientActor: "client-actor");

        resolution.ResolvedTome.ShouldBe("claim-actor");
        resolution.Source.ShouldBe(TomeSource.ClaimFile);
    }

    [Fact]
    public void Resolve_EmptyClaimBlock_FallsThroughToClient()
    {
        var resolution = _resolver.Resolve(claim: new ActorClaim(), clientActor: "client-actor");

        resolution.ResolvedTome.ShouldBe("client-actor");
        resolution.Source.ShouldBe(TomeSource.ClientInfo);
    }

    [Fact]
    public void Resolve_ClientInfo_BeforeDefault()
    {
        var resolution = _resolver.Resolve(clientActor: "Claude Code");

        resolution.ResolvedTome.ShouldBe("claude-code");
        resolution.Source.ShouldBe(TomeSource.ClientInfo);
    }

    [Fact]
    public void Resolve_NoCandidates_FallsBackToDefaultTome()
    {
        var resolution = _resolver.Resolve();

        resolution.ResolvedTome.ShouldBe(TomeKey.DefaultTome);
        resolution.Source.ShouldBe(TomeSource.Default);
    }

    [Fact]
    public void Resolve_UsesConfiguredDefaultTome()
    {
        var resolver = new TomeResolver(new MemoryEngineOptions { DefaultTome = "Product Assistants" });

        var resolution = resolver.Resolve();

        resolution.ResolvedTome.ShouldBe("product-assistants");
        resolution.Source.ShouldBe(TomeSource.Default);
    }

    [Theory]
    [InlineData("  ")]
    [InlineData("")]
    public void Resolve_WhitespaceCandidate_SkipsToNextStep(string blank)
    {
        var resolution = _resolver.Resolve(envTome: blank, envActor: "env-actor");

        resolution.ResolvedTome.ShouldBe("env-actor");
        resolution.Source.ShouldBe(TomeSource.EnvActor);
    }

    [Fact]
    public void Resolve_NullClaim_SkipsClaimStep()
    {
        var resolution = _resolver.Resolve(claim: null, clientActor: "client-actor");

        resolution.ResolvedTome.ShouldBe("client-actor");
        resolution.Source.ShouldBe(TomeSource.ClientInfo);
    }

    [Fact]
    public void Resolve_GarbageExplicitParam_Throws()
    {
        Should.Throw<ArgumentException>(() => _resolver.Resolve(explicitTome: "tome;drop"));
    }

    [Fact]
    public void Resolve_GarbageEnvActor_Throws()
    {
        Should.Throw<ArgumentException>(() => _resolver.Resolve(envActor: "home/tome"));
    }
}
