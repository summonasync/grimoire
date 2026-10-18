using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class StatsEndpointsTests(GrimoireApiFactory factory) : IClassFixture<GrimoireApiFactory>
{
    [Fact]
    public async Task Get_ReturnsStatusCountsPerScope()
    {
        factory.Reset();
        factory.Store.Seed("project journal", [1f, 0f]);
        factory.Store.Seed("project fact", [1f, 0f], kind: MemoryKind.Fact);
        factory.Store.Seed("session note", [1f, 0f], scope: MemoryScope.Session, expiresAt: FakeClock.DefaultStart.AddHours(2));
        factory.Store.Seed(
            "gone",
            [1f, 0f],
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "wrong",
            tombstonedAt: FakeClock.DefaultStart);

        var response = await factory.NewClient().GetAsync("/api/stats");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("active").GetInt64().ShouldBe(3);
        body.GetProperty("superseded").GetInt64().ShouldBe(0);
        body.GetProperty("tombstoned").GetInt64().ShouldBe(1);
        body.GetProperty("activeByScope").GetProperty("project").GetInt64().ShouldBe(2);
        body.GetProperty("activeByScope").GetProperty("session").GetInt64().ShouldBe(1);
        body.GetProperty("pendingCompaction").GetInt64().ShouldBe(0);
    }
}
