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

    [Fact]
    public async Task Post_Rename_MovesActiveRecordsWithCountsAndHumanActor()
    {
        factory.Reset();
        var first = factory.Store.Seed("alpha one", [1f, 0f], tome: "alpha");
        var second = factory.Store.Seed("alpha two", [1f, 0f], tome: "alpha", createdAt: FakeClock.DefaultStart.AddHours(1));
        factory.Store.Seed(
            "alpha buried",
            [1f, 0f],
            tome: "alpha",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "gone",
            tombstonedAt: FakeClock.DefaultStart);
        factory.Clock.Advance(TimeSpan.FromMinutes(2));

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/alpha/rename", new { to = "gamma" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("from").GetString().ShouldBe("alpha");
        body.GetProperty("to").GetString().ShouldBe("gamma");
        body.GetProperty("retomed").GetInt32().ShouldBe(2);

        var moved = factory.Store.Records.Single(record => record.Id == first.Id);
        factory.Store.Records.Single(record => record.Id == second.Id).Tome.ShouldBe("gamma");
        moved.Tome.ShouldBe("gamma");
        moved.Corrections.Single().Action.ShouldBe(CorrectionAction.Retome);
        moved.Corrections.Single().Actor.ShouldBe(MemoryActor.Human);
        factory.Store.Records.Single(record => record.Content == "alpha buried").Tome.ShouldBe("alpha");
    }

    [Fact]
    public async Task Post_Rename_OntoAnOccupiedTome_Returns409TomeExists()
    {
        factory.Reset();
        factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");
        factory.Store.Seed("beta note", [1f, 0f], tome: "beta");

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/alpha/rename", new { to = "beta" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadError(response)).code.ShouldBe("tome_exists");
        factory.Store.Records.Single(record => record.Content == "alpha note").Tome.ShouldBe("alpha");
        factory.Store.Records.Single(record => record.Content == "beta note").Tome.ShouldBe("beta");
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("Not Valid!!")]
    public async Task Post_Rename_WithAnInvalidTarget_Returns400InvalidTome(string to)
    {
        factory.Reset();
        factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/alpha/rename", new { to });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_tome");
        factory.Store.Records.Single().Tome.ShouldBe("alpha");
    }

    [Fact]
    public async Task Post_Rename_WithAMissingTarget_Returns400InvalidRequest()
    {
        factory.Reset();
        factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/alpha/rename", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_request");
        factory.Store.Records.Single().Tome.ShouldBe("alpha");
    }

    [Fact]
    public async Task Post_Rename_WithAnInvalidSourceKey_Returns400InvalidTome()
    {
        factory.Reset();

        var response = await factory.NewClient()
            .PostAsJsonAsync($"/api/tomes/{Uri.EscapeDataString("Not Valid!!")}/rename", new { to = "gamma" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_tome");
    }

    [Fact]
    public async Task Post_Merge_MovesActiveRecordsIntoTheTargetTome()
    {
        factory.Reset();
        var first = factory.Store.Seed("alpha one", [1f, 0f], tome: "alpha");
        factory.Store.Seed("alpha two", [1f, 0f], tome: "alpha", createdAt: FakeClock.DefaultStart.AddHours(1));
        factory.Store.Seed("beta note", [1f, 0f], tome: "beta");
        factory.Store.Seed(
            "beta buried",
            [1f, 0f],
            tome: "beta",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "gone",
            tombstonedAt: FakeClock.DefaultStart);
        factory.Clock.Advance(TimeSpan.FromMinutes(2));

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/alpha/merge", new { into = "Beta" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("from").GetString().ShouldBe("alpha");
        body.GetProperty("to").GetString().ShouldBe("beta");
        body.GetProperty("retomed").GetInt32().ShouldBe(2);

        factory.Store.Records.Single(record => record.Id == first.Id).Tome.ShouldBe("beta");
        factory.Store.Records.Count(record => record.Tome == "beta" && record.Status == MemoryStatus.Active).ShouldBe(3);
        factory.Store.Records.ShouldNotContain(record => record.Tome == "alpha" && record.Status == MemoryStatus.Active);
        factory.Store.Records.Single(record => record.Content == "beta buried").Tome.ShouldBe("beta");
    }

    [Fact]
    public async Task Post_Merge_IntoAMissingTome_Returns404WithoutMoving()
    {
        factory.Reset();
        factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/alpha/merge", new { into = "beta" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadError(response)).code.ShouldBe("not_found");
        factory.Store.Records.Single().Tome.ShouldBe("alpha");
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("Not Valid!!")]
    public async Task Post_Merge_WithAnInvalidTarget_Returns400InvalidTome(string into)
    {
        factory.Reset();
        factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/alpha/merge", new { into });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_tome");
        factory.Store.Records.Single().Tome.ShouldBe("alpha");
    }

    [Fact]
    public async Task Post_Retome_MovesListedRecordsAndReturnsCounts()
    {
        factory.Reset();
        var first = factory.Store.Seed("one", [1f, 0f]);
        factory.Store.Seed("two", [1f, 0f], createdAt: FakeClock.DefaultStart.AddHours(1));
        var third = factory.Store.Seed("three", [1f, 0f], createdAt: FakeClock.DefaultStart.AddHours(2));

        var response = await factory.NewClient()
            .PostAsJsonAsync("/api/tomes/records/retome", new { ids = new[] { first.Id, third.Id }, to = "gamma" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("retomed").GetInt32().ShouldBe(2);
        body.GetProperty("to").GetString().ShouldBe("gamma");

        factory.Store.Records.Single(record => record.Id == first.Id).Tome.ShouldBe("gamma");
        factory.Store.Records.Single(record => record.Id == third.Id).Tome.ShouldBe("gamma");
        factory.Store.Records.Single(record => record.Content == "two").Tome.ShouldBe(TomeKey.DefaultTome);
    }

    [Fact]
    public async Task Post_Retome_WithAnUnknownId_Returns404WithoutMovingAnything()
    {
        factory.Reset();
        var known = factory.Store.Seed("one", [1f, 0f]);

        var response = await factory.NewClient()
            .PostAsJsonAsync("/api/tomes/records/retome", new { ids = new[] { known.Id, Guid.NewGuid() }, to = "gamma" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadError(response)).code.ShouldBe("not_found");
        factory.Store.Records.Single().Tome.ShouldBe(TomeKey.DefaultTome);
    }

    [Fact]
    public async Task Post_Retome_WithNoIds_Returns400()
    {
        factory.Reset();

        var empty = await factory.NewClient()
            .PostAsJsonAsync("/api/tomes/records/retome", new { ids = Array.Empty<Guid>(), to = "gamma" });
        var missing = await factory.NewClient().PostAsJsonAsync("/api/tomes/records/retome", new { to = "gamma" });

        empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(empty)).code.ShouldBe("invalid_retome");
        missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(missing)).code.ShouldBe("invalid_request");
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("Not Valid!!")]
    public async Task Post_Retome_WithAnInvalidTarget_Returns400InvalidTome(string to)
    {
        factory.Reset();
        var known = factory.Store.Seed("one", [1f, 0f]);

        var response = await factory.NewClient()
            .PostAsJsonAsync("/api/tomes/records/retome", new { ids = new[] { known.Id }, to });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_tome");
        factory.Store.Records.Single().Tome.ShouldBe(TomeKey.DefaultTome);
    }

    [Fact]
    public async Task Post_Forget_TombstonesTheTomeAndReturnsCounts()
    {
        factory.Reset();
        var first = factory.Store.Seed("alpha one", [1f, 0f], tome: "alpha");
        factory.Store.Seed("alpha two", [1f, 0f], tome: "alpha", createdAt: FakeClock.DefaultStart.AddHours(1));
        factory.Store.Seed("beta note", [1f, 0f], tome: "beta");
        factory.Store.Seed(
            "alpha buried",
            [1f, 0f],
            tome: "alpha",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "gone",
            tombstonedAt: FakeClock.DefaultStart);
        factory.Clock.Advance(TimeSpan.FromMinutes(2));

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/alpha/forget", new { reason = "tome retired" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("tome").GetString().ShouldBe("alpha");
        body.GetProperty("forgotten").GetInt32().ShouldBe(2);

        var forgotten = factory.Store.Records.Single(record => record.Id == first.Id);
        forgotten.Status.ShouldBe(MemoryStatus.Tombstoned);
        forgotten.TombstoneReason.ShouldBe("tome retired");
        forgotten.Corrections.Single().Action.ShouldBe(CorrectionAction.Forget);
        forgotten.Corrections.Single().Actor.ShouldBe(MemoryActor.Human);
        factory.Store.Records.ShouldContain(record => record.Id == first.Id);
        factory.Store.Records.Single(record => record.Content == "beta note").Status.ShouldBe(MemoryStatus.Active);
        factory.Store.Records.Single(record => record.Content == "alpha buried").TombstoneReason.ShouldBe("gone");
    }

    [Fact]
    public async Task Post_Forget_OnAnEmptyTome_ReturnsZero()
    {
        factory.Reset();

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/ghost/forget", new { reason = "tome retired" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("tome").GetString().ShouldBe("ghost");
        body.GetProperty("forgotten").GetInt32().ShouldBe(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Post_Forget_WithAMissingReason_Returns400InvalidReason(string reason)
    {
        factory.Reset();
        factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/alpha/forget", new { reason });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_reason");
        factory.Store.Records.Single().Status.ShouldBe(MemoryStatus.Active);
    }

    [Fact]
    public async Task Post_Forget_WithAMissingReasonProperty_Returns400InvalidRequest()
    {
        factory.Reset();
        factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/alpha/forget", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_request");
        factory.Store.Records.Single().Status.ShouldBe(MemoryStatus.Active);
    }

    [Fact]
    public async Task Post_Forget_WithAnInvalidKey_Returns400InvalidTome()
    {
        factory.Reset();

        var response = await factory.NewClient()
            .PostAsJsonAsync($"/api/tomes/{Uri.EscapeDataString("Not Valid!!")}/forget", new { reason = "tome retired" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_tome");
    }

    private static async Task<(string code, string message)> ReadError(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var error = body.GetProperty("error");
        return (error.GetProperty("code").GetString()!, error.GetProperty("message").GetString()!);
    }
}
