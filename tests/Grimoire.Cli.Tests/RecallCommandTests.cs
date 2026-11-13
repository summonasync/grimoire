using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

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

    public void Dispose() => _host.Dispose();
}
