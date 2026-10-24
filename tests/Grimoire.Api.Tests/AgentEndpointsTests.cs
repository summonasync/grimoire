using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class AgentEndpointsTests(GrimoireApiFactory factory) : IClassFixture<GrimoireApiFactory>
{
    [Fact]
    public async Task Get_GroupsActorsByRecentActivity()
    {
        factory.Reset();
        factory.Clock.Advance(CacheTtlEpsilon);
        var now = factory.Clock.UtcNow;
        factory.Store.Seed("fresh write", [1f, 0f], actor: MemoryActor.Agent("tucker", "qwen"), createdAt: now.AddDays(-1));
        factory.Store.Seed("newer write", [1f, 0f], actor: MemoryActor.Agent("tucker", "qwen"), createdAt: now.AddHours(-2));
        factory.Store.Seed("human write", [1f, 0f], actor: MemoryActor.Human, createdAt: now.AddDays(-3));
        factory.Store.Seed("stale write", [1f, 0f], actor: MemoryActor.Agent("old-bot", "qwen"), createdAt: now.AddDays(-90));

        var response = await factory.NewClient().GetAsync("/api/agents");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("windowDays").GetInt32().ShouldBe(30);
        var agents = body.GetProperty("agents");
        agents.GetArrayLength().ShouldBe(2);
        agents[0].GetProperty("alias").GetString().ShouldBe("tucker");
        agents[0].GetProperty("vendor").GetString().ShouldBe("qwen");
        agents[0].GetProperty("recordCount").GetInt32().ShouldBe(2);
        agents[0].GetProperty("lastActivity").GetDateTimeOffset().ShouldBe(now.AddHours(-2), TimeSpan.FromSeconds(1));
        agents[0].GetProperty("isHuman").GetBoolean().ShouldBeFalse();
        agents[1].GetProperty("alias").GetString().ShouldBe("human");
        agents[1].GetProperty("isHuman").GetBoolean().ShouldBeTrue();
        agents[1].GetProperty("recordCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Get_CountsTombstonedWritesAsActivity()
    {
        factory.Reset();
        factory.Clock.Advance(CacheTtlEpsilon);
        var now = factory.Clock.UtcNow;
        factory.Store.Seed(
            "gone write",
            [1f, 0f],
            actor: MemoryActor.Agent("gone-bot", "qwen"),
            createdAt: now.AddHours(-5),
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "test",
            tombstonedAt: now.AddHours(-4));

        var response = await factory.NewClient().GetAsync("/api/agents");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var agents = body.GetProperty("agents");
        agents.GetArrayLength().ShouldBe(1);
        agents[0].GetProperty("alias").GetString().ShouldBe("gone-bot");
        agents[0].GetProperty("recordCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Get_ClampsWindowBounds()
    {
        factory.Reset();
        factory.Clock.Advance(CacheTtlEpsilon);
        var client = factory.NewClient();

        var low = await client.GetAsync("/api/agents?windowDays=0");
        var high = await client.GetAsync("/api/agents?windowDays=9999");

        (await low.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options))
            .GetProperty("windowDays").GetInt32().ShouldBe(1);
        (await high.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options))
            .GetProperty("windowDays").GetInt32().ShouldBe(365);
    }

    [Fact]
    public async Task Get_ServesCachedSnapshotUntilTtlExpires()
    {
        factory.Reset();
        factory.Clock.Advance(CacheTtlEpsilon);
        var client = factory.NewClient();

        _ = await client.GetAsync("/api/agents");
        factory.Store.Seed(
            "late write",
            [1f, 0f],
            actor: MemoryActor.Agent("late-bot", "qwen"),
            createdAt: factory.Clock.UtcNow);

        var cached = await (await client.GetAsync("/api/agents")).Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        cached.GetProperty("agents").GetArrayLength().ShouldBe(0);

        factory.Clock.Advance(TimeSpan.FromSeconds(31));
        var fresh = await (await client.GetAsync("/api/agents")).Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        fresh.GetProperty("agents").GetArrayLength().ShouldBe(1);
        fresh.GetProperty("agents")[0].GetProperty("alias").GetString().ShouldBe("late-bot");
    }

    private static TimeSpan CacheTtlEpsilon => TimeSpan.FromSeconds(31);
}
