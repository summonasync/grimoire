using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class ScryCommandTests : IDisposable
{
    private const string Query = "deploy pipeline";

    private readonly TestHost _host;

    public ScryCommandTests()
    {
        _host = new TestHost(new StubEmbeddings(new Dictionary<string, float[]> { [Query] = [1f, 0f] }));
    }

    [Fact]
    public async Task Scry_ByDefault_ExcludesOwnTomeAndSurfacesFactsOnly()
    {
        Seed("alpha fact on the pipeline", "alpha", actor: new MemoryActor("qwen", "toolcorp"));
        Seed("alpha journal on the pipeline", "alpha", kind: MemoryKind.Journal);
        Seed("own tome fact on the pipeline", "default");
        Seed("session note on the pipeline", "beta", scope: MemoryScope.Session);

        var code = await _host.RunAsync("scry", Query);

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("Tomes: alpha");
        output.ShouldContain("alpha fact on the pipeline");
        output.ShouldNotContain("alpha journal on the pipeline");
        output.ShouldNotContain("own tome fact on the pipeline");
        output.ShouldNotContain("session note on the pipeline");
        output.ShouldNotContain("qwen");

        var search = _host.Store.Searched.ShouldHaveSingleItem();
        search.Filter.Kind.ShouldBe(MemoryKind.Fact);
        search.Filter.Tomes.ShouldBe(["alpha", "beta"]);
    }

    [Fact]
    public async Task Scry_IncludeJournal_OptsIntoJournalEntries()
    {
        Seed("alpha fact on the pipeline", "alpha");
        Seed("alpha journal on the pipeline", "alpha", kind: MemoryKind.Journal);

        var code = await _host.RunAsync("scry", Query, "--include-journal");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("alpha journal on the pipeline");
        _host.Store.Searched.ShouldHaveSingleItem().Filter.Kind.ShouldBeNull();
    }

    [Fact]
    public async Task Scry_TomesFlag_NarrowsTheSearch()
    {
        Seed("alpha fact on the pipeline", "alpha");
        Seed("beta fact on the pipeline", "beta");

        var code = await _host.RunAsync("scry", Query, "--tomes", "beta");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("Tomes: beta");
        output.ShouldContain("beta fact on the pipeline");
        output.ShouldNotContain("alpha fact on the pipeline");
        _host.Store.Searched.ShouldHaveSingleItem().Filter.Tomes.ShouldBe(["beta"]);
    }

    [Fact]
    public async Task Scry_AttributionLight_ShowsAliasOnly()
    {
        Seed("alpha fact on the pipeline", "alpha", actor: new MemoryActor("qwen", "toolcorp"));

        var code = await _host.RunAsync("scry", Query, "--attribution", "light");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("by qwen");
        output.ShouldNotContain("toolcorp");
    }

    [Fact]
    public async Task Scry_AttributionFull_ShowsAliasAndVendor()
    {
        Seed("alpha fact on the pipeline", "alpha", actor: new MemoryActor("qwen", "toolcorp"));

        var code = await _host.RunAsync("scry", Query, "--attribution", "full");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("by qwen+toolcorp");
    }

    [Fact]
    public async Task Scry_LimitCapsResults()
    {
        Seed("strongest alpha fact", "alpha", vector: [1f, 0f]);
        Seed("middle alpha fact", "alpha", vector: [0.6f, 0.8f]);
        Seed("weakest alpha fact", "alpha", vector: [0f, 1f]);

        var code = await _host.RunAsync("scry", Query, "--limit", "2");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("strongest alpha fact");
        output.ShouldContain("middle alpha fact");
        output.ShouldNotContain("weakest alpha fact");
    }

    [Fact]
    public async Task Scry_WithoutOtherTomes_ReportsNone()
    {
        Seed("own tome fact on the pipeline", "default");

        var code = await _host.RunAsync("scry", Query);

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("No other tomes to scry.");
    }

    [Fact]
    public async Task Scry_WithoutMatches_ReportsEmptyHonesty()
    {
        Seed("beta session note on the pipeline", "beta", scope: MemoryScope.Session);

        var code = await _host.RunAsync("scry", Query);

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("Tomes: beta");
        _host.Output.ToString().ShouldContain("No matching memories in other tomes.");
    }

    [Fact]
    public async Task Scry_WithClaimFile_ExcludesTheTomeTheClaimResolves()
    {
        using var host = ClaimedHost();
        host.Store.Seed(ClaimRecord(host, "own claimed fact on the pipeline", "platform-work"), [1f, 0f]);
        host.Store.Seed(ClaimRecord(host, "alpha fact on the pipeline", "alpha"), [1f, 0f]);

        var code = await host.RunAsync("scry", Query);

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        var output = host.Output.ToString();
        output.ShouldContain("Tomes: alpha");
        output.ShouldContain("alpha fact on the pipeline");
        output.ShouldNotContain("own claimed fact on the pipeline");
    }

    [Theory]
    [InlineData("scry", Query, "--tomes", "Bad Tome!")]
    [InlineData("scry", Query, "--attribution", "secret")]
    [InlineData("scry", Query, "--limit", "0")]
    [InlineData("scry", "   ")]
    public async Task Scry_BadArguments_ExitWithUsage(params string[] args)
    {
        var code = await _host.RunAsync(args);

        code.ShouldBe(ExitCodes.Usage);
    }

    private TestHost ClaimedHost() => new(
        new StubEmbeddings(new Dictionary<string, float[]> { [Query] = [1f, 0f] }),
        claims: new FixedClaimProvider(new ActorClaim { Alias = "dan", Tome = "platform-work" }));

    private MemoryRecord ClaimRecord(TestHost host, string content, string tome) => new()
    {
        Id = Guid.NewGuid(),
        Content = content,
        Scope = MemoryScope.Project,
        Kind = MemoryKind.Fact,
        Actor = MemoryActor.Human,
        Tome = tome,
        CreatedAt = host.Clock.UtcNow,
        LastAccessedAt = host.Clock.UtcNow,
    };

    private void Seed(
        string content,
        string tome,
        MemoryKind kind = MemoryKind.Fact,
        MemoryScope scope = MemoryScope.Project,
        MemoryActor? actor = null,
        float[]? vector = null)
    {
        _host.Store.Seed(new MemoryRecord
        {
            Id = Guid.NewGuid(),
            Content = content,
            Scope = scope,
            Kind = kind,
            Actor = actor ?? MemoryActor.Human,
            Tome = tome,
            CreatedAt = _host.Clock.UtcNow,
            LastAccessedAt = _host.Clock.UtcNow,
        }, vector ?? [1f, 0f]);
    }

    public void Dispose() => _host.Dispose();
}
