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

    [Fact]
    public async Task Post_Rename_OntoATombstonedOnlyTome_Returns409TomeExists()
    {
        factory.Reset();
        factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");
        factory.Store.Seed(
            "beta buried",
            [1f, 0f],
            tome: "beta",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "gone",
            tombstonedAt: FakeClock.DefaultStart);
        var collisionCounts = new List<MemoryFilter>();
        factory.Store.OnCountAsync = collisionCounts.Add;

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/alpha/rename", new { to = "beta" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadError(response)).code.ShouldBe("tome_exists");
        factory.Store.Records.Single(record => record.Content == "alpha note").Tome.ShouldBe("alpha");
        var buried = factory.Store.Records.Single(record => record.Content == "beta buried");
        buried.Tome.ShouldBe("beta");
        buried.Status.ShouldBe(MemoryStatus.Tombstoned);

        factory.Store.OnCountAsync = null;
        var collision = collisionCounts.ShouldHaveSingleItem();
        collision.Tome.ShouldBe("beta");
        collision.Statuses.ShouldBe([MemoryStatus.Active, MemoryStatus.Superseded, MemoryStatus.Tombstoned]);
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

    [Fact]
    public async Task Post_Merge_IntoATombstonedOnlyTome_MovesSourceRecordsIn()
    {
        factory.Reset();
        var first = factory.Store.Seed("alpha one", [1f, 0f], tome: "alpha");
        factory.Store.Seed(
            "beta buried",
            [1f, 0f],
            tome: "beta",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "gone",
            tombstonedAt: FakeClock.DefaultStart);
        factory.Clock.Advance(TimeSpan.FromMinutes(2));
        var existenceCounts = new List<MemoryFilter>();
        factory.Store.OnCountAsync = existenceCounts.Add;

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/alpha/merge", new { into = "beta" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("retomed").GetInt32().ShouldBe(1);
        factory.Store.Records.Single(record => record.Id == first.Id).Tome.ShouldBe("beta");
        var buried = factory.Store.Records.Single(record => record.Content == "beta buried");
        buried.Tome.ShouldBe("beta");
        buried.Status.ShouldBe(MemoryStatus.Tombstoned);

        factory.Store.OnCountAsync = null;
        var existence = existenceCounts.Single(filter => filter.Tome == "beta");
        existence.Statuses.ShouldBe([MemoryStatus.Active, MemoryStatus.Superseded, MemoryStatus.Tombstoned]);
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

    [Fact]
    public async Task Get_UnionIncludesManifestOnlyTomesWithNullCensusFields()
    {
        factory.Reset();
        factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");
        factory.Store.Seed("home note", [1f, 0f]);
        factory.Manifests.Seed(new TomeManifest
        {
            Key = "env",
            Description = "machine and infra notes",
            CreatedBy = "coder+ai",
            CreatedAt = FakeClock.DefaultStart.AddDays(-3),
            UpdatedAt = FakeClock.DefaultStart.AddDays(-1),
        });

        var response = await factory.NewClient().GetAsync("/api/tomes");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var tomes = body.GetProperty("tomes");
        tomes.EnumerateArray().Select(tome => tome.GetProperty("key").GetString())
            .ShouldBe(["alpha", "default", "env"]);
        var env = tomes.EnumerateArray().Single(tome => tome.GetProperty("key").GetString() == "env");
        env.GetProperty("active").GetInt64().ShouldBe(0);
        env.GetProperty("tombstoned").GetInt64().ShouldBe(0);
        env.GetProperty("lastWriteAt").ValueKind.ShouldBe(JsonValueKind.Null);
        env.GetProperty("description").GetString().ShouldBe("machine and infra notes");
        env.GetProperty("createdBy").GetString().ShouldBe("coder+ai");
        env.GetProperty("manifest").GetBoolean().ShouldBeTrue();
        env.GetProperty("own").GetBoolean().ShouldBeFalse();
        var alpha = tomes.EnumerateArray().Single(tome => tome.GetProperty("key").GetString() == "alpha");
        alpha.GetProperty("manifest").GetBoolean().ShouldBeFalse();
        alpha.GetProperty("description").ValueKind.ShouldBe(JsonValueKind.Null);
        alpha.GetProperty("createdBy").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("tomes").EnumerateArray()
            .Single(tome => tome.GetProperty("key").GetString() == "default")
            .GetProperty("own").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Get_WhileTheManifestStoreIsDegraded_RendersCensusOnlyEntries()
    {
        factory.Reset();
        factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");
        factory.Manifests.Seed(new TomeManifest
        {
            Key = "alpha",
            Description = "alpha journal",
            CreatedBy = "coder+ai",
            CreatedAt = FakeClock.DefaultStart,
            UpdatedAt = FakeClock.DefaultStart,
        });
        factory.Manifests.Seed(new TomeManifest
        {
            Key = "env",
            Description = "machine and infra notes",
            CreatedBy = "human",
            CreatedAt = FakeClock.DefaultStart,
            UpdatedAt = FakeClock.DefaultStart,
        });
        factory.Manifests.DegradationReason = "Tome manifest store is unreachable — simulated outage.";

        var response = await factory.NewClient().GetAsync("/api/tomes");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var tomes = body.GetProperty("tomes");
        tomes.EnumerateArray().Select(tome => tome.GetProperty("key").GetString())
            .ShouldBe(["alpha"]);
        var alpha = tomes.EnumerateArray().Single(tome => tome.GetProperty("key").GetString() == "alpha");
        alpha.GetProperty("active").GetInt64().ShouldBe(1);
        alpha.GetProperty("manifest").GetBoolean().ShouldBeFalse();
        alpha.GetProperty("description").ValueKind.ShouldBe(JsonValueKind.Null);
        alpha.GetProperty("createdBy").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Post_Create_ReturnsTheFreshManifestWithHumanActor()
    {
        factory.Reset();

        var response = await factory.NewClient()
            .PostAsJsonAsync("/api/tomes", new { key = "env", description = "machine notes" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("key").GetString().ShouldBe("env");
        body.GetProperty("description").GetString().ShouldBe("machine notes");
        body.GetProperty("createdBy").GetString().ShouldBe("human");
        body.GetProperty("createdAt").GetDateTimeOffset().ShouldBe(factory.Clock.UtcNow);
        body.GetProperty("updatedAt").GetDateTimeOffset().ShouldBe(factory.Clock.UtcNow);
        factory.Manifests.Manifests.Single().Key.ShouldBe("env");
    }

    [Fact]
    public async Task Post_Create_OnAnExistingManifest_Returns409TomeManifestExists()
    {
        factory.Reset();
        factory.Manifests.Seed(new TomeManifest
        {
            Key = "env",
            Description = "original",
            CreatedBy = "human",
            CreatedAt = FakeClock.DefaultStart,
            UpdatedAt = FakeClock.DefaultStart,
        });

        var response = await factory.NewClient()
            .PostAsJsonAsync("/api/tomes", new { key = "env", description = "second attempt" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await ReadError(response);
        error.code.ShouldBe("tome_manifest_exists");
        error.message.ShouldContain("env");
        factory.Manifests.Manifests.Single().Description.ShouldBe("original");
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("Not Valid!!")]
    public async Task Post_Create_WithAnInvalidKey_Returns400InvalidTome(string key)
    {
        factory.Reset();

        var response = await factory.NewClient()
            .PostAsJsonAsync("/api/tomes", new { key, description = "machine notes" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await ReadError(response);
        error.code.ShouldBe("invalid_tome");
        error.message.ShouldContain("key");
        error.message.ShouldContain(key);
        factory.Manifests.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_Create_WithAnEmptyDescription_Returns400InvalidDescription()
    {
        factory.Reset();

        var response = await factory.NewClient()
            .PostAsJsonAsync("/api/tomes", new { key = "env", description = "   " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await ReadError(response);
        error.code.ShouldBe("invalid_description");
        error.message.ShouldContain("description");
        error.message.ShouldContain("1..280");
        factory.Manifests.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_Create_WithAnOverLongDescription_Returns400InvalidDescription()
    {
        factory.Reset();

        var response = await factory.NewClient()
            .PostAsJsonAsync("/api/tomes", new { key = "env", description = new string('x', 300) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await ReadError(response);
        error.code.ShouldBe("invalid_description");
        error.message.ShouldContain("1..280");
        error.message.ShouldContain("300");
        factory.Manifests.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_Create_WithAMissingDescriptionProperty_Returns400InvalidDescription()
    {
        factory.Reset();

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes", new { key = "env" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await ReadError(response);
        error.code.ShouldBe("invalid_description");
        error.message.ShouldContain("description");
        error.message.ShouldContain("1..280");
        factory.Manifests.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_Create_WithAMissingKeyProperty_Returns400InvalidTome()
    {
        factory.Reset();

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes", new { description = "machine notes" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await ReadError(response);
        error.code.ShouldBe("invalid_tome");
        error.message.ShouldContain("key");
        factory.Manifests.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Patch_UpdatesTheDescriptionAndPreservesProvenance()
    {
        factory.Reset();
        factory.Manifests.Seed(new TomeManifest
        {
            Key = "env",
            Description = "before",
            CreatedBy = "coder+ai",
            CreatedAt = FakeClock.DefaultStart.AddDays(-5),
            UpdatedAt = FakeClock.DefaultStart.AddDays(-5),
        });
        factory.Clock.Advance(TimeSpan.FromDays(1));

        var response = await factory.NewClient()
            .PatchAsJsonAsync("/api/tomes/env/manifest", new { description = "rewritten" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("key").GetString().ShouldBe("env");
        body.GetProperty("description").GetString().ShouldBe("rewritten");
        body.GetProperty("createdBy").GetString().ShouldBe("coder+ai");
        body.GetProperty("createdAt").GetDateTimeOffset().ShouldBe(FakeClock.DefaultStart.AddDays(-5));
        body.GetProperty("updatedAt").GetDateTimeOffset().ShouldBe(factory.Clock.UtcNow);
        factory.Manifests.Manifests.Single().Description.ShouldBe("rewritten");
    }

    [Fact]
    public async Task Patch_OnAMissingManifest_Returns404TomeManifestNotFound()
    {
        factory.Reset();
        factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");

        var response = await factory.NewClient()
            .PatchAsJsonAsync("/api/tomes/alpha/manifest", new { description = "rewritten" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var error = await ReadError(response);
        error.code.ShouldBe("tome_manifest_not_found");
        error.message.ShouldContain("alpha");
    }

    [Fact]
    public async Task Patch_WithAnOverLongDescription_Returns400InvalidDescriptionAndLeavesTheManifestUntouched()
    {
        factory.Reset();
        factory.Manifests.Seed(new TomeManifest
        {
            Key = "env",
            Description = "before",
            CreatedBy = "human",
            CreatedAt = FakeClock.DefaultStart,
            UpdatedAt = FakeClock.DefaultStart,
        });

        var response = await factory.NewClient()
            .PatchAsJsonAsync("/api/tomes/env/manifest", new { description = new string('x', 280) + "!" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_description");
        factory.Manifests.Manifests.Single().Description.ShouldBe("before");
    }

    [Fact]
    public async Task Patch_WhenTheManifestStoreIsDegraded_Returns503UpstreamUnavailable()
    {
        factory.Reset();
        factory.Manifests.DegradationReason = "Tome manifest store is unreachable — simulated outage.";

        var response = await factory.NewClient()
            .PatchAsJsonAsync("/api/tomes/env/manifest", new { description = "rewritten" });

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var error = await ReadError(response);
        error.code.ShouldBe("upstream_unavailable");
        error.message.ShouldContain("unreachable");
        factory.Manifests.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_WithAWrongConfirm_Returns400ConfirmMismatchWithoutDeletingAnything()
    {
        factory.Reset();
        factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");
        factory.Store.Seed(
            "alpha buried",
            [1f, 0f],
            tome: "alpha",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "gone",
            tombstonedAt: FakeClock.DefaultStart);
        factory.Manifests.Seed(new TomeManifest
        {
            Key = "alpha",
            Description = "alpha journal",
            CreatedBy = "human",
            CreatedAt = FakeClock.DefaultStart,
            UpdatedAt = FakeClock.DefaultStart,
        });
        var censusBefore = await (await factory.NewClient().GetAsync("/api/tomes"))
            .Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var recordCountBefore = factory.Store.Records.Count;

        var response = await factory.NewClient().DeleteAsync("/api/tomes/alpha?confirm=nope");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await ReadError(response);
        error.code.ShouldBe("confirm_mismatch");
        error.message.ShouldContain("alpha");
        factory.Store.Records.Count.ShouldBe(recordCountBefore);
        factory.Manifests.Manifests.ShouldNotBeEmpty();

        var censusAfter = await (await factory.NewClient().GetAsync("/api/tomes"))
            .Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        censusAfter.GetProperty("tomes").EnumerateArray()
            .Select(tome => (tome.GetProperty("key").GetString(), tome.GetProperty("active").GetInt64()))
            .ShouldBe(censusBefore.GetProperty("tomes").EnumerateArray()
                .Select(tome => (tome.GetProperty("key").GetString(), tome.GetProperty("active").GetInt64())));
    }

    [Fact]
    public async Task Delete_WithAWrongConfirm_OnATombstonedOnlyTome_Returns400WithoutDeletingAnything()
    {
        factory.Reset();
        factory.Store.Seed(
            "beta buried",
            [1f, 0f],
            tome: "beta",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "gone",
            tombstonedAt: FakeClock.DefaultStart);

        var response = await factory.NewClient().DeleteAsync("/api/tomes/beta?confirm=nope");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("confirm_mismatch");
        factory.Store.Records.Single().Content.ShouldBe("beta buried");
    }

    [Fact]
    public async Task Delete_PurgesRecordsInAllStatusesAndTheManifestWithPerStatusCounts()
    {
        factory.Reset();
        factory.Store.Seed("alpha one", [1f, 0f], tome: "alpha");
        factory.Store.Seed("alpha two", [1f, 0f], tome: "alpha");
        factory.Store.Seed(
            "alpha buried",
            [1f, 0f],
            tome: "alpha",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "gone",
            tombstonedAt: FakeClock.DefaultStart);
        factory.Store.Seed(
            "alpha stale",
            [1f, 0f],
            tome: "alpha",
            status: MemoryStatus.Superseded,
            supersededBy: Guid.NewGuid());
        factory.Manifests.Seed(new TomeManifest
        {
            Key = "alpha",
            Description = "alpha journal",
            CreatedBy = "human",
            CreatedAt = FakeClock.DefaultStart,
            UpdatedAt = FakeClock.DefaultStart,
        });

        var response = await factory.NewClient().DeleteAsync("/api/tomes/alpha?confirm=alpha");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("key").GetString().ShouldBe("alpha");
        body.GetProperty("manifestPurged").GetBoolean().ShouldBeTrue();
        var purged = body.GetProperty("purged");
        purged.GetProperty("active").GetInt32().ShouldBe(2);
        purged.GetProperty("tombstoned").GetInt32().ShouldBe(1);
        purged.GetProperty("superseded").GetInt32().ShouldBe(1);
        factory.Store.Records.ShouldBeEmpty();
        factory.Manifests.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_PurgesATombstonedOnlyTome_AndDropsItFromTheCensus()
    {
        factory.Reset();
        factory.Store.Seed("home note", [1f, 0f]);
        factory.Store.Seed(
            "beta buried",
            [1f, 0f],
            tome: "beta",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "gone",
            tombstonedAt: FakeClock.DefaultStart);
        var guardCounts = new List<MemoryFilter>();
        factory.Store.OnCountAsync = guardCounts.Add;

        var response = await factory.NewClient().DeleteAsync("/api/tomes/beta?confirm=beta");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("key").GetString().ShouldBe("beta");
        body.GetProperty("manifestPurged").GetBoolean().ShouldBeTrue();
        factory.Manifests.Manifests.ShouldBeEmpty();
        var purged = body.GetProperty("purged");
        purged.GetProperty("active").GetInt32().ShouldBe(0);
        purged.GetProperty("tombstoned").GetInt32().ShouldBe(1);
        purged.GetProperty("superseded").GetInt32().ShouldBe(0);
        factory.Store.Records.Single().Content.ShouldBe("home note");

        factory.Store.OnCountAsync = null;
        var guard = guardCounts.ShouldHaveSingleItem();
        guard.Tome.ShouldBe("beta");
        guard.Statuses.ShouldBe([MemoryStatus.Active, MemoryStatus.Superseded, MemoryStatus.Tombstoned]);

        var census = await (await factory.NewClient().GetAsync("/api/tomes"))
            .Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        census.GetProperty("tomes").EnumerateArray()
            .ShouldNotContain(tome => tome.GetProperty("key").GetString() == "beta");
    }

    [Fact]
    public async Task Delete_OnAPartiallyPurgedTome_ConvergesWithZeroCounts()
    {
        factory.Reset();
        factory.Store.Seed("alpha one", [1f, 0f], tome: "alpha");
        factory.Manifests.Seed(new TomeManifest
        {
            Key = "alpha",
            Description = "alpha journal",
            CreatedBy = "human",
            CreatedAt = FakeClock.DefaultStart,
            UpdatedAt = FakeClock.DefaultStart,
        });
        factory.Manifests.DeleteFailure = new InvalidOperationException("manifest collection is down");

        var first = await factory.NewClient().DeleteAsync("/api/tomes/alpha?confirm=alpha");

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        firstBody.GetProperty("purged").GetProperty("active").GetInt32().ShouldBe(1);
        firstBody.GetProperty("manifestPurged").GetBoolean().ShouldBeFalse();
        factory.Store.Records.ShouldBeEmpty();
        factory.Manifests.Manifests.ShouldNotBeEmpty();

        factory.Manifests.DeleteFailure = null;
        var second = await factory.NewClient().DeleteAsync("/api/tomes/alpha?confirm=alpha");

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        secondBody.GetProperty("purged").GetProperty("active").GetInt32().ShouldBe(0);
        secondBody.GetProperty("purged").GetProperty("tombstoned").GetInt32().ShouldBe(0);
        secondBody.GetProperty("purged").GetProperty("superseded").GetInt32().ShouldBe(0);
        secondBody.GetProperty("manifestPurged").GetBoolean().ShouldBeTrue();
        factory.Manifests.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_OnAnUnknownTome_Returns404WithoutTouchingAnything()
    {
        factory.Reset();
        factory.Store.Seed("home note", [1f, 0f]);

        var response = await factory.NewClient().DeleteAsync("/api/tomes/ghost?confirm=ghost");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadError(response)).code.ShouldBe("not_found");
        factory.Store.Records.Single().Tome.ShouldBe(TomeKey.DefaultTome);
    }

    [Fact]
    public async Task Delete_WhenTheManifestStoreIsDegraded_Returns503UpstreamUnavailableWithoutDeletingAnything()
    {
        factory.Reset();
        factory.Store.Seed("home note", [1f, 0f]);
        factory.Manifests.DegradationReason = "Tome manifest store is unreachable — simulated outage.";

        var response = await factory.NewClient().DeleteAsync("/api/tomes/ghost?confirm=ghost");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var error = await ReadError(response);
        error.code.ShouldBe("upstream_unavailable");
        error.message.ShouldContain("unreachable");
        factory.Store.Records.Single().Content.ShouldBe("home note");
        factory.Manifests.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_Rename_MovesTheManifestPreservingCreatedByAndCreatedAt()
    {
        factory.Reset();
        factory.Store.Seed("alpha one", [1f, 0f], tome: "alpha");
        factory.Store.Seed("alpha two", [1f, 0f], tome: "alpha");
        factory.Manifests.Seed(new TomeManifest
        {
            Key = "alpha",
            Description = "alpha journal",
            CreatedBy = "coder+ai",
            CreatedAt = FakeClock.DefaultStart.AddDays(-5),
            UpdatedAt = FakeClock.DefaultStart.AddDays(-5),
        });
        factory.Clock.Advance(TimeSpan.FromMinutes(2));

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/alpha/rename", new { to = "gamma" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("manifestMoved").GetBoolean().ShouldBeTrue();
        body.GetProperty("manifestDeleted").ValueKind.ShouldBe(JsonValueKind.Null);

        var moved = factory.Manifests.Manifests.Single();
        moved.Key.ShouldBe("gamma");
        moved.Description.ShouldBe("alpha journal");
        moved.CreatedBy.ShouldBe("coder+ai");
        moved.CreatedAt.ShouldBe(FakeClock.DefaultStart.AddDays(-5));
        moved.UpdatedAt.ShouldBe(factory.Clock.UtcNow);
        factory.Store.Records.ShouldAllBe(record => record.Tome == "gamma");
    }

    [Fact]
    public async Task Post_Rename_WithoutASourceManifest_ReportsManifestMovedTrue()
    {
        factory.Reset();
        factory.Store.Seed("alpha one", [1f, 0f], tome: "alpha");

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/alpha/rename", new { to = "gamma" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("manifestMoved").GetBoolean().ShouldBeTrue();
        factory.Manifests.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_Rename_WithAManifestOnTheTarget_Returns409TomeManifestExistsWithoutMoving()
    {
        factory.Reset();
        factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");
        factory.Manifests.Seed(new TomeManifest
        {
            Key = "beta",
            Description = "beta journal",
            CreatedBy = "human",
            CreatedAt = FakeClock.DefaultStart,
            UpdatedAt = FakeClock.DefaultStart,
        });

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/alpha/rename", new { to = "beta" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await ReadError(response);
        error.code.ShouldBe("tome_manifest_exists");
        error.message.ShouldContain("beta");
        factory.Store.Records.Single().Tome.ShouldBe("alpha");
        factory.Manifests.Manifests.Single().Key.ShouldBe("beta");
    }

    [Fact]
    public async Task Post_Merge_DeletesTheSourceManifestAndKeepsTheTargetManifest()
    {
        factory.Reset();
        factory.Store.Seed("alpha one", [1f, 0f], tome: "alpha");
        factory.Store.Seed("alpha two", [1f, 0f], tome: "alpha");
        factory.Store.Seed("beta note", [1f, 0f], tome: "beta");
        factory.Manifests.Seed(new TomeManifest
        {
            Key = "alpha",
            Description = "alpha journal",
            CreatedBy = "human",
            CreatedAt = FakeClock.DefaultStart,
            UpdatedAt = FakeClock.DefaultStart,
        });
        factory.Manifests.Seed(new TomeManifest
        {
            Key = "beta",
            Description = "beta journal",
            CreatedBy = "human",
            CreatedAt = FakeClock.DefaultStart,
            UpdatedAt = FakeClock.DefaultStart,
        });

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/alpha/merge", new { into = "beta" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("manifestDeleted").GetBoolean().ShouldBeTrue();
        body.GetProperty("manifestMoved").ValueKind.ShouldBe(JsonValueKind.Null);
        factory.Manifests.Manifests.Single().Key.ShouldBe("beta");
        factory.Store.Records.ShouldAllBe(record => record.Tome == "beta");
    }

    [Fact]
    public async Task Post_Merge_WithoutASourceManifest_ReportsManifestDeletedTrue()
    {
        factory.Reset();
        factory.Store.Seed("alpha one", [1f, 0f], tome: "alpha");
        factory.Store.Seed("beta note", [1f, 0f], tome: "beta");

        var response = await factory.NewClient().PostAsJsonAsync("/api/tomes/alpha/merge", new { into = "beta" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("manifestDeleted").GetBoolean().ShouldBeTrue();
        factory.Manifests.Manifests.ShouldBeEmpty();
    }

    private static async Task<(string code, string message)> ReadError(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var error = body.GetProperty("error");
        return (error.GetProperty("code").GetString()!, error.GetProperty("message").GetString()!);
    }
}
