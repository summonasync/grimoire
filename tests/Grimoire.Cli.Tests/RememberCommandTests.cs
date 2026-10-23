using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class RememberCommandTests : IDisposable
{
    private readonly TestHost _host = new(
        new StubEmbeddings(new Dictionary<string, float[]>
        {
            ["prefers dark mode"] = [1f, 0f],
            ["chose qdrant"] = [1f, 0f],
            ["retry loop"] = [1f, 0f],
            ["wip idea"] = [1f, 0f],
            ["long term note"] = [1f, 0f],
            ["note"] = [1f, 0f],
        }),
        projects: TestServices.DefaultProjects);

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

    [Fact]
    public async Task Remember_ProjectScopeWithCallerPathMatch_AutotagsTheRegisteredProject()
    {
        var code = await _host.RunAsync("remember", "prefers dark mode");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Store.Upserted.ShouldHaveSingleItem().ProjectId.ShouldBe("sandbox");
        _host.Output.ToString().ShouldContain("project 'sandbox' auto-tagged from the working directory");
    }

    [Fact]
    public async Task Remember_ProjectScopeWithoutProjectIdAndNoPathMatch_ExitsUsageWithoutWriting()
    {
        using var host = NoMatchHost(TestServices.DefaultProjects);

        var code = await host.RunAsync("remember", "long term note");

        code.ShouldBe(ExitCodes.Usage);
        var output = host.Output.ToString();
        output.ShouldContain("error [project_id_required]");
        output.ShouldContain("'/opt/nowhere' matches no registered project path");
        output.ShouldContain("grimoire, sandbox");
        output.ShouldContain("--scope global");
        host.Store.Upserted.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remember_UnknownProject_ExitsUsageWithTheRegisteredSet()
    {
        var code = await _host.RunAsync("remember", "long term note", "--project", "Slurpy.AI");

        code.ShouldBe(ExitCodes.Usage);
        var output = _host.Output.ToString();
        output.ShouldContain("error [project_id_unknown]");
        output.ShouldContain("'Slurpy.AI' is not a registered project alias");
        output.ShouldContain("Allowed --project values (case-sensitive, pass one exactly as advertised): grimoire, sandbox");
        _host.Store.Upserted.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remember_EmptyProjectRegistry_RejectsWithoutNamingAliases()
    {
        using var host = NoMatchHost([]);

        var code = await host.RunAsync("remember", "long term note");

        code.ShouldBe(ExitCodes.Usage);
        var output = host.Output.ToString();
        output.ShouldContain("error [project_id_required]");
        output.ShouldContain("The project registry is empty");
        output.ShouldNotContain("Allowed --project values");
        host.Store.Upserted.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(MemoryScope.Global)]
    [InlineData(MemoryScope.Personal)]
    public async Task Remember_LongTermScopeWithoutProject_WritesWithoutTag(MemoryScope scope)
    {
        var code = await _host.RunAsync("remember", "long term note", "--scope", ToFlag(scope));

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Store.Upserted.ShouldHaveSingleItem().ProjectId.ShouldBeNull();
    }

    [Theory]
    [InlineData(MemoryScope.Global)]
    [InlineData(MemoryScope.Personal)]
    public async Task Remember_LongTermScopeWithUnknownProject_ExitsUsage(MemoryScope scope)
    {
        var code = await _host.RunAsync("remember", "long term note", "--scope", ToFlag(scope), "--project", "Slurpy.AI");

        code.ShouldBe(ExitCodes.Usage);
        _host.Output.ToString().ShouldContain("error [project_id_unknown]");
        _host.Store.Upserted.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remember_SessionScopeWithProject_ExitsUsageWithoutWriting()
    {
        var code = await _host.RunAsync("remember", "long term note", "--scope", "session", "--project", "grimoire");

        code.ShouldBe(ExitCodes.Usage);
        var output = _host.Output.ToString();
        output.ShouldContain("error [param_conflict]");
        output.ShouldContain("--project only applies to scopes 'project', 'global' and 'personal'");
        output.ShouldContain("(got --project 'grimoire' with scope 'session')");
        _host.Store.Upserted.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remember_SessionScopeWithoutProject_WritesWithoutTag()
    {
        var code = await _host.RunAsync("remember", "wip idea", "--scope", "session", "--session", "s1");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var record = _host.Store.Upserted.ShouldHaveSingleItem();
        record.Scope.ShouldBe(MemoryScope.Session);
        record.ProjectId.ShouldBeNull();
    }

    [Theory]
    [InlineData(MemoryScope.Project)]
    [InlineData(MemoryScope.Global)]
    [InlineData(MemoryScope.Personal)]
    public async Task Remember_NonSessionScopeWithSession_ExitsUsageWithoutWriting(MemoryScope scope)
    {
        var code = await _host.RunAsync("remember", "long term note", "--scope", ToFlag(scope), "--session", "s1");

        code.ShouldBe(ExitCodes.Usage);
        var output = _host.Output.ToString();
        output.ShouldContain("error [param_conflict]");
        output.ShouldContain("--session only applies to scope 'session'");
        output.ShouldContain($"(got --session 's1' with scope '{ToFlag(scope)}')");
        _host.Store.Upserted.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remember_SessionScopeWithoutSessionId_WritesAnonymousSessionRecord()
    {
        var code = await _host.RunAsync("remember", "wip idea", "--scope", "session");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var record = _host.Store.Upserted.ShouldHaveSingleItem();
        record.Scope.ShouldBe(MemoryScope.Session);
        record.SessionId.ShouldBeNull();
    }

    private static TestHost NoMatchHost(IReadOnlyList<ProjectAliasRecord> projects) => new(
        new StubEmbeddings(new Dictionary<string, float[]> { ["long term note"] = [1f, 0f] }),
        callerPaths: new FixedCallerPath("/opt/nowhere"),
        projects: projects);

    private static string ToFlag(MemoryScope scope) => scope.ToString().ToLowerInvariant();

    private static TestHost TomedHost(
        IReadOnlyDictionary<string, string>? variables,
        IActorClaimProvider? claims = null) =>
        new(
            new StubEmbeddings(new Dictionary<string, float[]> { ["prefers dark mode"] = [1f, 0f], ["chose qdrant"] = [1f, 0f] }),
            claims: claims,
            variables: variables,
            projects: TestServices.DefaultProjects);

    public void Dispose() => _host.Dispose();
}
