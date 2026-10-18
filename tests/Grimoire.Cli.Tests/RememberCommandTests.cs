using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class RememberCommandTests : IDisposable
{
    private readonly TestHost _host = new(new StubEmbeddings(new Dictionary<string, float[]>
    {
        ["prefers dark mode"] = [1f, 0f],
        ["chose qdrant"] = [1f, 0f],
        ["retry loop"] = [1f, 0f],
        ["wip idea"] = [1f, 0f],
        ["long term note"] = [1f, 0f],
        ["note"] = [1f, 0f],
    }));

    [Fact]
    public async Task Remember_DefaultsToProjectScopeJournalAndHumanActor()
    {
        var code = await _host.RunAsync("remember", "prefers dark mode");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var record = _host.Store.Upserted.ShouldHaveSingleItem();
        record.Scope.ShouldBe(MemoryScope.Project);
        record.Kind.ShouldBe(MemoryKind.Journal);
        record.Actor.ShouldBe(MemoryActor.Human);
        record.Importance.ShouldBe(0.5);
    }

    [Fact]
    public async Task Remember_CarriesActorFromFlag()
    {
        var code = await _host.RunAsync("remember", "chose qdrant", "--actor", "junior+claude");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Store.Upserted.ShouldHaveSingleItem().Actor.ShouldBe(new MemoryActor("junior", "claude"));
    }

    [Fact]
    public async Task Remember_ParsesTagsImportanceAndIds()
    {
        var code = await _host.RunAsync(
            "remember", "retry loop", "--scope", "global", "--kind", "fact",
            "--tags", "networking,retries", "--importance", "0.9", "--project", "grimoire");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var record = _host.Store.Upserted.ShouldHaveSingleItem();
        record.Scope.ShouldBe(MemoryScope.Global);
        record.Kind.ShouldBe(MemoryKind.Fact);
        record.Tags.ShouldBe(["networking", "retries"]);
        record.Importance.ShouldBe(0.9);
        record.ProjectId.ShouldBe("grimoire");
    }

    [Fact]
    public async Task Remember_TtlOverridesSessionExpiry()
    {
        var code = await _host.RunAsync("remember", "wip idea", "--scope", "session", "--session", "s1", "--ttl", "3");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Store.Updated.ShouldHaveSingleItem().ExpiresAt.ShouldBe(_host.Clock.UtcNow.AddHours(3));
    }

    [Fact]
    public async Task Remember_TtlOutsideSessionScope_ExitsUsageWithoutWriting()
    {
        var code = await _host.RunAsync("remember", "long term note", "--ttl", "3");

        code.ShouldBe(ExitCodes.Usage);
        _host.Store.Upserted.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remember_IdenticalRetry_ReportsReplayWithoutNewWrite()
    {
        await _host.RunAsync("remember", "wip idea", "--scope", "global");

        var code = await _host.RunAsync("remember", "wip idea", "--scope", "global");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("Replayed an existing record");
        _host.Store.Upserted.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Remember_MaintenanceFailsAfterCommit_ReportsPendingAndExitsOk()
    {
        await _host.RunAsync("remember", "prefers dark mode");
        _host.Store.UpdateFailure = new HttpRequestException("supersede update failed");

        var code = await _host.RunAsync("remember", "chose qdrant");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("Remembered");
        output.ShouldContain("duplicate maintenance is pending");
        _host.Store.Upserted.Count.ShouldBe(2);
        _host.Store.Records.Count(record => record.Status == MemoryStatus.Active).ShouldBe(2);
    }

    [Theory]
    [InlineData("--scope", "galactic")]
    [InlineData("--kind", "rumor")]
    [InlineData("--importance", "2")]
    [InlineData("--ttl", "0")]
    public async Task Remember_InvalidFlagValue_ExitsUsage(string flag, string value)
    {
        var code = await _host.RunAsync("remember", "note", flag, value);

        code.ShouldBe(ExitCodes.Usage);
    }

    [Fact]
    public async Task Remember_EnvTomeLandsTheWriteThere()
    {
        using var host = TomedHost(new Dictionary<string, string> { ["GRIMOIRE_TOME"] = "platform-work" });

        var code = await host.RunAsync("remember", "prefers dark mode");

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        host.Store.Upserted.ShouldHaveSingleItem().Tome.ShouldBe("platform-work");
        host.Output.ToString().ShouldContain("in tome 'platform-work'");
    }

    [Fact]
    public async Task Remember_ExplicitTomeOverridesEnvTome()
    {
        using var host = TomedHost(new Dictionary<string, string> { ["GRIMOIRE_TOME"] = "platform-work" });

        var code = await host.RunAsync("remember", "chose qdrant", "--tome", "research");

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        host.Store.Upserted.ShouldHaveSingleItem().Tome.ShouldBe("research");
    }

    [Fact]
    public async Task Remember_EnvActorImpliesTomeAndCarriesTheActor()
    {
        using var host = TomedHost(new Dictionary<string, string> { [ActorResolver.EnvironmentVariable] = "dan+claude" });

        var code = await host.RunAsync("remember", "prefers dark mode");

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        var record = host.Store.Upserted.ShouldHaveSingleItem();
        record.Tome.ShouldBe("dan");
        record.Actor.ShouldBe(new MemoryActor("dan", "claude"));
    }

    [Fact]
    public async Task Remember_ClaimFileBindsTomeAndAliasWithoutFlagsOrEnv()
    {
        using var host = TomedHost(null, new FixedClaimProvider(new ActorClaim { Alias = "dan", Tome = "platform-work" }));

        var code = await host.RunAsync("remember", "prefers dark mode");

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        var record = host.Store.Upserted.ShouldHaveSingleItem();
        record.Tome.ShouldBe("platform-work");
        record.Actor.ShouldBe(new MemoryActor("dan", null));
    }

    [Fact]
    public async Task Remember_WithoutIdentity_LandsInTheConfiguredDefaultTome()
    {
        var code = await _host.RunAsync("remember", "prefers dark mode");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Store.Upserted.ShouldHaveSingleItem().Tome.ShouldBe("default");
    }

    [Fact]
    public async Task Remember_InvalidTomeFlag_ExitsUsageWithoutWriting()
    {
        var code = await _host.RunAsync("remember", "note", "--tome", "Not Valid!");

        code.ShouldBe(ExitCodes.Usage);
        _host.Store.Upserted.ShouldBeEmpty();
    }

    private static TestHost TomedHost(
        IReadOnlyDictionary<string, string>? variables,
        IActorClaimProvider? claims = null) =>
        new(
            new StubEmbeddings(new Dictionary<string, float[]> { ["prefers dark mode"] = [1f, 0f], ["chose qdrant"] = [1f, 0f] }),
            claims: claims,
            variables: variables);

    public void Dispose() => _host.Dispose();
}
