using System.Text.Json;
using Grimoire.Api.Contracts;
using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

// The Api search door now delegates widening to RecallEscalationLoop, so these fixtures pin the
// arithmetic of that delegation plus the warnings channel. The date-driven flood leans on the
// clause this fake does not enforce (CreatedBefore is pushed down by the real translator since
// 055): it proves the Api door reaches the shared home, not that production floods this way.
public class MemoryQueryRunnerEscalationTests(GrimoireApiFactory factory) : IClassFixture<GrimoireApiFactory>
{
    private const string Query = "vector";
    private const string UntilBound = "2026-10-17T12:00:00Z";

    [Fact]
    public async Task Search_RejectFloodDeeperThanOneDoubling_WidensUntilThePageFills()
    {
        factory.Reset();
        factory.Embeddings.Vectors[Query] = [1f, 0f];
        SeedFlood(8);
        var wanted = factory.Store.Seed("deep honest match", [0.5f, 0.866f], createdAt: FakeClock.DefaultStart.AddDays(-1));

        var response = await ReadList($"/api/memories?query={Query}&limit=1&until={UntilBound}");

        response.Items.Select(record => record.Id).ShouldBe([wanted.Id]);
        factory.Store.SearchCallCount.ShouldBe(3);
        factory.Store.SearchWindows.ShouldBe([3, 6, 12]);
        response.Warnings.ShouldBeNull();
    }

    [Fact]
    public async Task Search_EveryWindowRejectedUntilTheCap_ResponseCarriesTheWarning()
    {
        factory.Reset();
        factory.Embeddings.Vectors[Query] = [1f, 0f];
        var rejects = Enumerable.Range(1, 24)
            .Select(index => factory.Store.Seed(
                $"flooder {index}",
                [0.99f - index * 0.001f, 0.141f],
                actor: MemoryActor.Agent("flooder", "claude")));

        // Was built on `actor=flooder+`: that grammar filled the window and let the matcher empty
        // it, which is the divergence 061 removed. The door still has to be able to shout, so the
        // reject-full window is injected the way the scry fixtures do it — through the hook that
        // hands back rows the store clause would never keep.
        factory.Store.RejectingWindow = [.. rejects];

        var response = await ReadList($"/api/memories?query={Query}&limit=1&actor=dotnet-coder");

        response.Items.ShouldBeEmpty();
        factory.Store.SearchCallCount.ShouldBe(RecallEscalationLoop.MaxRounds);
        factory.Store.SearchWindows.ShouldBe([3, 6, 12, 24]);
        response.Warnings.ShouldNotBeNull();
        response.Warnings!.ShouldHaveSingleItem();
        response.Warnings[0].ShouldContain("after 4 rounds");
        response.Warnings[0].ShouldContain("0 of limit 1 matches");
    }

    // One escalation scan per requested status: the Superseded scan can run out of rounds while the
    // Active scan fills the page. A page that is full must not shout "came up short" — that is the
    // per-status accumulator leaking past Take(limit).
    [Fact]
    public async Task Search_CappedStatusFillsNoPageButTheSiblingDoes_ResponseStaysSilent()
    {
        factory.Reset();
        factory.Embeddings.Vectors[Query] = [1f, 0f];
        SeedSupersededFlood(48);
        factory.Store.Seed("active one", [0.9f, 0.4359f], createdAt: FakeClock.DefaultStart.AddDays(-1));
        factory.Store.Seed("active two", [0.8f, 0.6f], createdAt: FakeClock.DefaultStart.AddDays(-1));

        var response = await ReadList(
            $"/api/memories?query={Query}&limit=2&status=active,superseded&until={UntilBound}");

        response.Count.ShouldBe(2);
        response.Items.Count.ShouldBe(2);
        response.Warnings.ShouldBeNull();
        factory.Store.SearchCallCount.ShouldBe(5);
        factory.Store.SearchWindows.ShouldContain(48);
    }

    [Fact]
    public async Task Search_CappedStatusLeavesThePagePartial_ResponseStillCarriesTheWarning()
    {
        factory.Reset();
        factory.Embeddings.Vectors[Query] = [1f, 0f];
        SeedSupersededFlood(72);
        factory.Store.Seed("active one", [0.9f, 0.4359f], createdAt: FakeClock.DefaultStart.AddDays(-1));
        factory.Store.Seed("active two", [0.8f, 0.6f], createdAt: FakeClock.DefaultStart.AddDays(-1));

        var response = await ReadList(
            $"/api/memories?query={Query}&limit=3&status=active,superseded&until={UntilBound}");

        response.Count.ShouldBe(2);
        response.Warnings.ShouldNotBeNull();
        response.Warnings!.ShouldHaveSingleItem();
        response.Warnings[0].ShouldContain("0 of limit 3 matches");
    }

    [Fact]
    public async Task Search_ZeroLimit_ParserClampsToOneBeforeTheLoopSeesIt()
    {
        factory.Reset();
        factory.Embeddings.Vectors[Query] = [1f, 0f];
        var kept = factory.Store.Seed("kept note", [1f, 0f], createdAt: FakeClock.DefaultStart.AddDays(-1));

        var response = await ReadList($"/api/memories?query={Query}&limit=0&until={UntilBound}");

        response.Items.Select(record => record.Id).ShouldBe([kept.Id]);
        factory.Store.SearchWindows.ShouldNotBeEmpty();
        factory.Store.SearchWindows[0].ShouldBe(3);
    }

    [Fact]
    public async Task Search_ActorWashoutAfterStorePushdown_NeedsNoWidening()
    {
        factory.Reset();
        factory.Embeddings.Vectors[Query] = [1f, 0f];
        factory.Store.Seed("decoy 1", [0.9950f, 0.0998f], actor: MemoryActor.Human);
        factory.Store.Seed("decoy 2", [0.9899f, 0.1421f], actor: MemoryActor.Human);
        factory.Store.Seed("decoy 3", [0.9848f, 0.1736f], actor: MemoryActor.Human);
        factory.Store.Seed("decoy 4", [0.9798f, 0.2000f], actor: MemoryActor.Human);
        factory.Store.Seed("decoy 5", [0.9747f, 0.2234f], actor: MemoryActor.Human);
        factory.Store.Seed("decoy 6", [0.9696f, 0.2449f], actor: MemoryActor.Human);
        var first = factory.Store.Seed("deep one", [0.8660f, 0.5f]);
        var second = factory.Store.Seed("deep two", [0.4f, 0.9165f]);

        var response = await ReadList($"/api/memories?query={Query}&limit=2&actor=dotnet-coder");

        response.Items.Select(record => record.Id).ShouldBe([first.Id, second.Id]);
        factory.Store.SearchCallCount.ShouldBe(1);
    }

    // Rows the fake store hands back but MemoryFilterExtensions.Matches drops (they sit on the
    // until bound). A status only reaches the round cap while every round still comes back full,
    // so the count has to cover the last window: limit x RecallOversampling(3) x 2^(MaxRounds-1).
    private void SeedSupersededFlood(int rejects)
    {
        for (var index = 1; index <= rejects; index++)
        {
            var angle = index * 2f * MathF.PI / 180f;
            factory.Store.Seed(
                $"superseded noise {index}",
                [MathF.Cos(angle), MathF.Sin(angle)],
                createdAt: FakeClock.DefaultStart,
                status: MemoryStatus.Superseded);
        }
    }

    private void SeedFlood(int rejects)
    {
        for (var index = 1; index <= rejects; index++)
        {
            var angle = index * 3.5f * MathF.PI / 180f;
            factory.Store.Seed(
                $"noise {index}",
                [MathF.Cos(angle), MathF.Sin(angle)],
                createdAt: FakeClock.DefaultStart);
        }
    }

    private async Task<MemoryListResponse> ReadList(string path)
    {
        using var response = await factory.NewClient().GetAsync(path);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        return (await JsonSerializer.DeserializeAsync<MemoryListResponse>(stream, TestJson.Options))!;
    }
}
