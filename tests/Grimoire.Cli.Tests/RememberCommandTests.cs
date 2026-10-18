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

    public void Dispose() => _host.Dispose();
}
