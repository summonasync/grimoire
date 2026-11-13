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
        for (var index = 1; index <= 24; index++)
        {
            factory.Store.Seed(
                $"flooder {index}",
                [0.99f - index * 0.001f, 0.141f],
                actor: MemoryActor.Agent("flooder", "claude"));
        }

        var response = await ReadList(
            $"/api/memories?query={Query}&limit=1&actor={Uri.EscapeDataString("flooder+")}");

        response.Items.ShouldBeEmpty();
        factory.Store.SearchCallCount.ShouldBe(RecallEscalationLoop.MaxRounds);
        factory.Store.SearchWindows.ShouldBe([3, 6, 12, 24]);
        response.Warnings.ShouldNotBeNull();
        response.Warnings!.ShouldHaveSingleItem();
        response.Warnings[0].ShouldContain("after 4 rounds");
        response.Warnings[0].ShouldContain("0 of limit 1 matches");
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
