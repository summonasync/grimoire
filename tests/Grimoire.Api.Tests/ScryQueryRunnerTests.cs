using Grimoire.Api.Contracts;
using Grimoire.Api.Querying;
using Grimoire.Api.Tests.Fakes;
using Grimoire.Api.Tomes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class ScryQueryRunnerTests
{
    private static readonly TimeSpan DiscoveryDelay = TimeSpan.FromMilliseconds(40);

    [Fact]
    public async Task RunAsync_ReportsTheWallTimeOfDiscoveryAndRecall()
    {
        var store = new DelayedDiscoveryStore();
        store.Seed("alpha squad decision", [1f, 0f], kind: MemoryKind.Fact, tome: "alpha");
        var embeddings = new FakeEmbeddingProvider();
        embeddings.Vectors["alpha squad decision"] = [1f, 0f];
        var runner = new ScryQueryRunner(
            new MemoryService(store, embeddings, new FakeClock()),
            store,
            new ApiTomeResolver(new TomeResolver(), null, null, null));

        var response = await runner.RunAsync(
            new ParsedScryQuery("alpha squad decision", [], false, ScryAttribution.None, 5),
            CancellationToken.None);

        response.Tomes.ShouldBe(["alpha"]);
        response.Count.ShouldBe(1);
        response.ElapsedMs.ShouldBeGreaterThanOrEqualTo(30);
    }

    private sealed class DelayedDiscoveryStore : FakeMemoryStore
    {
        public override async Task<IReadOnlyList<MemoryRecord>> ListAsync(MemoryFilter filter, CancellationToken cancellationToken)
        {
            await Task.Delay(DiscoveryDelay, cancellationToken);
            return await base.ListAsync(filter, cancellationToken);
        }
    }
}
