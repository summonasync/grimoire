using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class TomeEndpointsTests(GrimoireApiFactory factory) : IClassFixture<GrimoireApiFactory>
{
    [Fact]
    public async Task Get_ListsEveryCensusTomeWithCountsAndLastWrite()
    {
        factory.Reset();
        var ancient = FakeClock.DefaultStart.AddDays(-30);
        factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha", createdAt: ancient);
        factory.Store.Seed(
            "alpha gone",
            [1f, 0f],
            tome: "alpha",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "wrong",
            tombstonedAt: FakeClock.DefaultStart);
        factory.Store.Seed("home note", [1f, 0f]);

        var response = await factory.NewClient().GetAsync("/api/tomes");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("tome").GetString().ShouldBe("default");
        var tomes = body.GetProperty("tomes");
        tomes.GetArrayLength().ShouldBe(2);
        tomes.EnumerateArray().Select(tome => tome.GetProperty("key").GetString())
            .ShouldBe(["alpha", "default"]);
        var alpha = tomes.EnumerateArray().Single(tome => tome.GetProperty("key").GetString() == "alpha");
        alpha.GetProperty("active").GetInt64().ShouldBe(1);
        alpha.GetProperty("tombstoned").GetInt64().ShouldBe(1);
        alpha.GetProperty("lastWriteAt").GetDateTimeOffset().ShouldBe(FakeClock.DefaultStart);
        alpha.GetProperty("own").GetBoolean().ShouldBeFalse();
        var home = tomes.EnumerateArray().Single(tome => tome.GetProperty("key").GetString() == "default");
        home.GetProperty("active").GetInt64().ShouldBe(1);
        home.GetProperty("own").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Get_OnAnEmptyStore_ReturnsAnEmptyListWithTheResolvedTome()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync("/api/tomes");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("tome").GetString().ShouldBe("default");
        body.GetProperty("tomes").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Get_MarksTheEnvironmentTomeAsOwn()
    {
        using var tomeFactory = GrimoireApiFactory.Create(envTome: "eng");
        tomeFactory.Reset();
        tomeFactory.Store.Seed("eng note", [1f, 0f], tome: "eng");
        tomeFactory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");

        var response = await tomeFactory.NewClient().GetAsync("/api/tomes");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("tome").GetString().ShouldBe("eng");
        var tomes = body.GetProperty("tomes");
        tomes.EnumerateArray().Single(tome => tome.GetProperty("key").GetString() == "eng")
            .GetProperty("own").GetBoolean().ShouldBeTrue();
        tomes.EnumerateArray().Single(tome => tome.GetProperty("key").GetString() == "alpha")
            .GetProperty("own").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Get_KeysMatchTheStatsByTomeKeys()
    {
        factory.Reset();
        factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");
        factory.Store.Seed("beta buried", [1f, 0f], tome: "beta", status: MemoryStatus.Tombstoned, tombstoneReason: "gone", tombstonedAt: FakeClock.DefaultStart);
        factory.Store.Seed("home note", [1f, 0f]);

        var tomes = await factory.NewClient().GetAsync("/api/tomes");
        var stats = await factory.NewClient().GetAsync("/api/stats");

        var tomeKeys = (await tomes.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options))
            .GetProperty("tomes").EnumerateArray()
            .Select(tome => tome.GetProperty("key").GetString())
            .OrderBy(name => name, StringComparer.Ordinal);
        var statsKeys = (await stats.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options))
            .GetProperty("byTome").EnumerateObject()
            .Select(entry => entry.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        tomeKeys.ShouldBe(statsKeys);
    }

    [Fact]
    public async Task Get_IncludesTombstoneOnlyTomes()
    {
        factory.Reset();
        factory.Store.Seed(
            "buried",
            [1f, 0f],
            tome: "beta",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "gone",
            tombstonedAt: FakeClock.DefaultStart);

        var response = await factory.NewClient().GetAsync("/api/tomes");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var beta = body.GetProperty("tomes").EnumerateArray()
            .Single(tome => tome.GetProperty("key").GetString() == "beta");
        beta.GetProperty("active").GetInt64().ShouldBe(0);
        beta.GetProperty("tombstoned").GetInt64().ShouldBe(1);
    }
}
