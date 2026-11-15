using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class ScryCommandWarningsTests : IDisposable
{
    private const string Query = "deploy pipeline";

    private readonly TestHost _host = new(new StubEmbeddings(new Dictionary<string, float[]> { [Query] = [1f, 0f] }));

    [Fact]
    public async Task Scry_RoundsRanOutWithAPartialPage_PrintsTheWarningAfterTheRows()
    {
        var kept = Seed("beta fact on the pipeline", "beta");
        _host.Store.RejectingWindow = [kept, .. Enumerable.Range(1, 48)
            .Select(index => Seed($"gamma fact {index}", "gamma"))];

        var code = await _host.RunAsync("scry", Query, "--tomes", "beta", "--limit", "2");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("beta fact on the pipeline");
        _host.Store.Searched.Count.ShouldBe(RecallEscalationLoop.MaxRounds);
        output.ShouldContain("Warning:");
        output.ShouldContain("1 of limit 2 matches");
    }

    [Fact]
    public async Task Scry_StoreCameBackShort_PrintsNoWarning()
    {
        Seed("beta fact on the pipeline", "beta");

        var code = await _host.RunAsync("scry", Query, "--tomes", "beta");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Store.Searched.ShouldHaveSingleItem();
        _host.Output.ToString().ShouldNotContain("Warning:");
    }

    private MemoryRecord Seed(string content, string tome)
    {
        var record = new MemoryRecord
        {
            Id = Guid.NewGuid(),
            Content = content,
            Scope = MemoryScope.Project,
            Kind = MemoryKind.Fact,
            Actor = MemoryActor.Human,
            Tome = tome,
            CreatedAt = _host.Clock.UtcNow,
            LastAccessedAt = _host.Clock.UtcNow,
        };
        _host.Store.Seed(record, tome == "beta" ? [1f, 0f] : [MathF.Cos(0.35f), MathF.Sin(0.35f)]);
        return record;
    }

    public void Dispose() => _host.Dispose();
}
