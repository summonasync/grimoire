using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

// Merged 2026-11-15: the tome-resolution pins came from feat/046-brief-sessionstart-hook, the cap-warning
// pins from feat/061-recall-escalation-loop. Both sides added this file at the same path with the same
// class name, and both halves of the shared prologue were identical, so the merge keeps five tests in one
// class rather than choosing a side.
//
// The CLI doors expose no flag that can produce a window full of rejects (recall takes scope and
// limit, scry takes tomes/journal/attribution/limit), so the capped fixtures inject the looser
// store clause through RecordingMemoryStore.RejectingWindow.
public sealed class RecallCommandTests : IDisposable
{
    private const string Query = "deploy pipeline";

    private readonly TestHost _host;

    public RecallCommandTests()
    {
        _host = new TestHost(new StubEmbeddings(new Dictionary<string, float[]> { [Query] = [1f, 0f] }));
    }

    [Fact]
    public async Task Recall_WithoutEnvResolvesTheEngineDefaultTome()
    {
        Seed(_host, "fact in the default tome", "default");
        Seed(_host, "fact in another tome", "platform-work");

        var code = await _host.RunAsync("recall", Query);

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Store.Searched.ShouldHaveSingleItem().Filter.Tome.ShouldBe("default");
        var output = _host.Output.ToString();
        output.ShouldContain("fact in the default tome");
        output.ShouldNotContain("fact in another tome");
    }

    [Fact]
    public async Task Recall_EnvTomeLandsTheReadThere()
    {
        using var host = new TestHost(
            new StubEmbeddings(new Dictionary<string, float[]> { [Query] = [1f, 0f] }),
            variables: new Dictionary<string, string> { ["GRIMOIRE_TOME"] = "platform-work" });
        Seed(host, "fact in the default tome", "default");
        Seed(host, "fact in the env tome", "platform-work");

        var code = await host.RunAsync("recall", Query);

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        host.Store.Searched.ShouldHaveSingleItem().Filter.Tome.ShouldBe("platform-work");
        var output = host.Output.ToString();
        output.ShouldContain("fact in the env tome");
        output.ShouldNotContain("fact in the default tome");
    }

    [Fact]
    public async Task Recall_RoundsRanOutWithNothingKept_PrintsTheWarning()
    {
        _host.Store.RejectingWindow = Rejects(24);

        var code = await _host.RunAsync("recall", Query, "--limit", "1");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("No matching memories.");
        _host.Store.Searched.Count.ShouldBe(RecallEscalationLoop.MaxRounds);
        output.ShouldContain("Warning: The page came up short after 4 rounds of window doubling");
        output.ShouldContain("a wider limit only widens the searched window, it cannot fill the page");
    }

    [Fact]
    public async Task Recall_RoundsRanOutWithAPartialPage_PrintsTheWarningBesideTheRows()
    {
        var kept = _host.Store.Add("the one honest match", [1f, 0f], scope: MemoryScope.Project, kind: MemoryKind.Fact);
        _host.Store.RejectingWindow = [kept, .. Rejects(48)];

        var code = await _host.RunAsync("recall", Query, "--limit", "2");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("the one honest match");
        output.ShouldNotContain("No matching memories.");
        output.ShouldContain("Warning:");
        output.ShouldContain("1 of limit 2 matches");
    }

    [Fact]
    public async Task Recall_StoreCameBackShort_PrintsNoWarning()
    {
        _host.Store.Add("a match", [1f, 0f], scope: MemoryScope.Project, kind: MemoryKind.Fact);

        var code = await _host.RunAsync("recall", Query);

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Store.Searched.ShouldHaveSingleItem();
        _host.Output.ToString().ShouldNotContain("Warning:");
    }

    private static void Seed(TestHost host, string content, string tome)
    {
        host.Store.Seed(
            new MemoryRecord
            {
                Id = Guid.NewGuid(),
                Content = content,
                Scope = MemoryScope.Project,
                Kind = MemoryKind.Fact,
                Actor = MemoryActor.Human,
                Tome = tome,
                CreatedAt = host.Clock.UtcNow,
                LastAccessedAt = host.Clock.UtcNow,
            },
            [1f, 0f]);
    }

    private IReadOnlyList<MemoryRecord> Rejects(int count)
    {
        var angle = 14f * MathF.PI / 180f;
        var vector = new[] { MathF.Cos(angle), MathF.Sin(angle) };
        return [.. Enumerable.Range(1, count)
            .Select(index => _host.Store.Add($"noise {index}", vector, scope: MemoryScope.Session))];
    }

    public void Dispose() => _host.Dispose();
}
