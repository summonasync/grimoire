using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

public sealed class MemoryActorProviderTests
{
    [Fact]
    public async Task GetActor_TheConfiguredFlag_OutsranksEveryChannel()
    {
        var session = new SessionIdentityHolder();
        session.Set(new MemoryActor("session-alias", "session-vendor"));

        var provider = Provider(
            configured: new MemoryActor("flag-alias", "flag-vendor"),
            sessionIdentity: session,
            identityFile: new FakeAgentIdentitySource(new MemoryActor("file-alias", "file-vendor")),
            envActor: "env-alias+env-vendor",
            claims: new FakeActorClaimProvider(new ActorClaim { Alias = "claim-alias" }));

        var actor = await provider.GetActorAsync(CancellationToken.None);

        actor.ShouldBe(new MemoryActor("flag-alias", "flag-vendor"));
    }

    [Fact]
    public async Task GetActor_TheSessionIdentity_OutsranksTheFileEnvAndClaimChannels()
    {
        var session = new SessionIdentityHolder();
        session.Set(new MemoryActor("session-alias", "session-vendor"));

        var provider = Provider(
            sessionIdentity: session,
            identityFile: new FakeAgentIdentitySource(new MemoryActor("file-alias", "file-vendor")),
            envActor: "env-alias+env-vendor",
            claims: new FakeActorClaimProvider(new ActorClaim { Alias = "claim-alias" }));

        var actor = await provider.GetActorAsync(CancellationToken.None);

        actor.ShouldBe(new MemoryActor("session-alias", "session-vendor"));
    }

    [Fact]
    public async Task GetActor_TheIdentityFile_OutsranksTheEnvAndClaimChannels()
    {
        var provider = Provider(
            identityFile: new FakeAgentIdentitySource(new MemoryActor("file-alias", "file-vendor")),
            envActor: "env-alias+env-vendor",
            claims: new FakeActorClaimProvider(new ActorClaim { Alias = "claim-alias" }));

        var actor = await provider.GetActorAsync(CancellationToken.None);

        actor.ShouldBe(new MemoryActor("file-alias", "file-vendor"));
    }

    [Fact]
    public async Task GetActor_TheEnvActor_OutsranksTheClaimAndClientChannels()
    {
        var provider = Provider(
            envActor: "env-alias+env-vendor",
            claims: new FakeActorClaimProvider(new ActorClaim { Alias = "claim-alias" }));

        var actor = await provider.GetActorAsync(CancellationToken.None);

        actor.ShouldBe(new MemoryActor("env-alias", "env-vendor"));
    }

    [Theory]
    [InlineData("env-alias")]
    [InlineData("env-alias+")]
    public async Task GetActor_AnEnvActorWithoutVendor_CarriesNullVendor(string envActor)
    {
        var provider = Provider(envActor: envActor);

        var actor = await provider.GetActorAsync(CancellationToken.None);

        actor.ShouldBe(new MemoryActor("env-alias", null));
    }

    [Fact]
    public async Task GetActor_ABlankEnvActor_FallsThroughToTheClaim()
    {
        var provider = Provider(
            envActor: "   ",
            claims: new FakeActorClaimProvider(new ActorClaim { Alias = "claim-alias" }));

        var actor = await provider.GetActorAsync(CancellationToken.None);

        actor.ShouldBe(new MemoryActor("claim-alias", null));
    }

    [Fact]
    public async Task GetActor_TheClaimAlias_OutsranksUnknown()
    {
        var provider = Provider(claims: new FakeActorClaimProvider(new ActorClaim { Alias = "claim-alias" }));

        var actor = await provider.GetActorAsync(CancellationToken.None);

        actor.ShouldBe(new MemoryActor("claim-alias", null));
    }

    [Fact]
    public async Task GetActor_EveryChannelEmpty_FallsBackToUnknown()
    {
        var provider = Provider();

        var actor = await provider.GetActorAsync(CancellationToken.None);

        actor.ShouldBe(new MemoryActor("unknown", null));
    }

    private static MemoryActorProvider Provider(
        MemoryActor? configured = null,
        SessionIdentityHolder? sessionIdentity = null,
        IAgentIdentitySource? identityFile = null,
        string? envActor = null,
        IActorClaimProvider? claims = null)
    {
        return new MemoryActorProvider(
            configured,
            null,
            envActor,
            sessionIdentity ?? new SessionIdentityHolder(),
            identityFile ?? new UnsetAgentIdentitySource(),
            new ClientInfoHolder(),
            claims ?? new FakeActorClaimProvider(),
            new TomeResolver());
    }
}
