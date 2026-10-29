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
        body.GetProperty("idempotencyConflicts").GetInt64().ShouldBe(0);
        body.GetProperty("tome").GetString().ShouldBe("default");
        body.GetProperty("byTome").GetProperty("default").GetProperty("active").GetInt64().ShouldBe(3);
        body.GetProperty("gc").GetProperty("stale").GetBoolean().ShouldBeTrue();
        body.GetProperty("gc").GetProperty("unknownReason").GetString().ShouldBe("gc state file does not exist yet");
    }

    [Fact]
    public async Task Get_CarriesTheGcHeartbeatWithTheStaleFlag()
    {
        factory.Reset();
        factory.GcState.Current = new GcState
        {
            LastAttemptAt = FakeClock.DefaultStart.AddHours(-1),
            LastSuccessAt = FakeClock.DefaultStart.AddHours(-1),
            RemovedExpired = 2,
            RemovedTombstones = 1,
        };

        var response = await factory.NewClient().GetAsync("/api/stats");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var gc = body.GetProperty("gc");
        gc.GetProperty("stale").GetBoolean().ShouldBeFalse();
        gc.GetProperty("lastSuccessAt").GetDateTimeOffset().ShouldBe(FakeClock.DefaultStart.AddHours(-1));
        gc.GetProperty("removedExpired").GetInt32().ShouldBe(2);
        gc.GetProperty("removedTombstones").GetInt32().ShouldBe(1);
        gc.GetProperty("lastError").ValueKind.ShouldBe(JsonValueKind.Null);
        gc.GetProperty("unknownReason").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Get_CarriesTheConsolidationHeartbeat()
    {
        factory.Reset();
        factory.ConsolidationState.Current = new ConsolidationState
        {
            LastAttemptAt = FakeClock.DefaultStart.AddHours(-1),
            LastSuccessAt = FakeClock.DefaultStart.AddHours(-1),
            ConsolidatedClusters = 3,
        };

        var response = await factory.NewClient().GetAsync("/api/stats");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var consolidation = body.GetProperty("consolidation");
        consolidation.GetProperty("stale").GetBoolean().ShouldBeFalse();
        consolidation.GetProperty("lastSuccessAt").GetDateTimeOffset().ShouldBe(FakeClock.DefaultStart.AddHours(-1));
        consolidation.GetProperty("consolidatedClusters").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task Get_ReportsAnUnconfiguredConsolidationHeartbeatAsUnknown()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync("/api/stats");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var consolidation = body.GetProperty("consolidation");
        consolidation.GetProperty("stale").GetBoolean().ShouldBeTrue();
        consolidation.GetProperty("unknownReason").GetString().ShouldBe("consolidation state file does not exist yet");
    }

    [Fact]
    public async Task Get_FlagsTheGcHeartbeatStalePastTwiceTheSweepInterval()
    {
        factory.Reset();
        factory.GcState.Current = new GcState
        {
            LastAttemptAt = FakeClock.DefaultStart.AddHours(-5),
            LastSuccessAt = FakeClock.DefaultStart.AddHours(-5),
        };

        var response = await factory.NewClient().GetAsync("/api/stats");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("gc").GetProperty("stale").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Get_EchoesTheTomeResolvedFromEnvironment()
    {
        using var tomeFactory = GrimoireApiFactory.Create(envTome: "eng");
        tomeFactory.Reset();

        var response = await tomeFactory.NewClient().GetAsync("/api/stats");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("tome").GetString().ShouldBe("eng");
    }

    [Fact]
    public async Task Get_ReportsTheExplicitParamSourceWhenTheParamIsPresent()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync("/api/stats?tome=eng");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("tome").GetString().ShouldBe("eng");
        body.GetProperty("tomeSource").GetString().ShouldBe("explicitParam");
    }

    [Fact]
    public async Task Get_ReportsTheEnvSourceWhenOnlyTheEnvironmentResolves()
    {
        using var tomeFactory = GrimoireApiFactory.Create(envTome: "eng");
        tomeFactory.Reset();

        var response = await tomeFactory.NewClient().GetAsync("/api/stats");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("tomeSource").GetString().ShouldBe("envTome");
    }

    [Fact]
    public async Task Get_ReportsTheDefaultSourceWhenNoHintIsPresent()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync("/api/stats");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("tomeSource").GetString().ShouldBe("default");
    }

    [Fact]
    public async Task Get_SplitsCountsByTome()
    {
        factory.Reset();
        factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");
        factory.Store.Seed(
            "alpha gone",
            [1f, 0f],
            tome: "alpha",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "wrong",
            tombstonedAt: FakeClock.DefaultStart);
        factory.Store.Seed("home note", [1f, 0f]);

        var response = await factory.NewClient().GetAsync("/api/stats");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("byTome").GetProperty("alpha").GetProperty("active").GetInt64().ShouldBe(1);
        body.GetProperty("byTome").GetProperty("alpha").GetProperty("tombstoned").GetInt64().ShouldBe(1);
        body.GetProperty("byTome").GetProperty("default").GetProperty("active").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Get_WithGarbageTome_Returns400()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync("/api/stats?tome=Not%20Valid%21%21");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options))
            .GetProperty("error").GetProperty("code").GetString().ShouldBe("invalid_tome");
    }

    [Fact]
    public async Task Get_EmitsLastWriteAtOnEveryTomeKey()
    {
        factory.Reset();
        var ancient = FakeClock.DefaultStart.AddDays(-30);
        factory.Store.Seed("alpha plain", [1f, 0f], tome: "alpha", createdAt: ancient.AddDays(10));
        factory.Store.Seed("default plain", [1f, 0f], createdAt: ancient.AddDays(5));

        var response = await factory.NewClient().GetAsync("/api/stats");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var byTome = body.GetProperty("byTome");

        byTome.EnumerateObject().Select(entry => entry.Name).OrderBy(name => name, StringComparer.Ordinal)
            .ShouldBe(["alpha", "default"]);
        byTome.EnumerateObject().Count(entry => entry.Value.TryGetProperty("lastWriteAt", out _))
            .ShouldBe(2);
        byTome.GetProperty("alpha").GetProperty("lastWriteAt").GetDateTimeOffset().ShouldBe(ancient.AddDays(10));
        byTome.GetProperty("alpha").GetProperty("active").GetInt64().ShouldBe(1);
        byTome.GetProperty("alpha").GetProperty("tombstoned").GetInt64().ShouldBe(0);
        byTome.GetProperty("default").GetProperty("lastWriteAt").GetDateTimeOffset().ShouldBe(ancient.AddDays(5));
    }

    [Fact]
    public async Task Get_LastWriteAtIsTheCorrectionEvenOnADecrepitRecord()
    {
        factory.Reset();
        var created = FakeClock.DefaultStart.AddDays(-30);
        var patched = FakeClock.DefaultStart.AddDays(-2);
        factory.Store.Seed(
            TestMemories.Create("stale fact", createdAt: created, tome: "alpha") with
            {
                LastAccessedAt = patched.AddDays(1),
                Corrections = [Patch(created.AddDays(1), "first fix"), Patch(patched, "second fix")],
            },
            [1f, 0f]);
        factory.Store.Seed("younger plain", [1f, 0f], tome: "alpha", createdAt: created.AddDays(20));

        var response = await factory.NewClient().GetAsync("/api/stats");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var alpha = body.GetProperty("byTome").GetProperty("alpha");
        alpha.GetProperty("lastWriteAt").GetDateTimeOffset().ShouldBe(patched);
        alpha.GetProperty("active").GetInt64().ShouldBe(2);
    }

    [Fact]
    public async Task Get_LastWriteAtPaysNoAttentionToTheReadStamp()
    {
        factory.Reset();
        var created = FakeClock.DefaultStart.AddDays(-30);
        factory.Store.Seed(
            TestMemories.Create("read a thousand times", createdAt: created, tome: "alpha") with
            {
                LastAccessedAt = FakeClock.DefaultStart.AddDays(40),
                AccessCount = 1000,
            },
            [1f, 0f]);
        factory.Store.Seed("younger rewrite", [1f, 0f], tome: "alpha", createdAt: FakeClock.DefaultStart.AddDays(-5));

        var response = await factory.NewClient().GetAsync("/api/stats");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("byTome").GetProperty("alpha").GetProperty("lastWriteAt").GetDateTimeOffset()
            .ShouldBe(FakeClock.DefaultStart.AddDays(-5));
    }

    [Fact]
    public async Task Get_LastWriteAtIsTheTombstoneWhenTheBurialIsTheNewestWrite()
    {
        factory.Reset();
        factory.Store.Seed("young active", [1f, 0f], tome: "alpha", createdAt: FakeClock.DefaultStart.AddDays(-20));
        factory.Store.Seed(
            "old buried",
            [1f, 0f],
            tome: "alpha",
            createdAt: FakeClock.DefaultStart.AddDays(-30),
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "gone",
            tombstonedAt: FakeClock.DefaultStart.AddDays(-1));

        var response = await factory.NewClient().GetAsync("/api/stats");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("byTome").GetProperty("alpha").GetProperty("lastWriteAt").GetDateTimeOffset()
            .ShouldBe(FakeClock.DefaultStart.AddDays(-1));
        body.GetProperty("byTome").GetProperty("alpha").GetProperty("tombstoned").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Get_BreaksDownActiveEntriesByProject()
    {
        factory.Reset();
        factory.Store.Seed(TestMemories.Create("overmind fact", tome: "alpha") with { ProjectId = "overmind" }, [1f, 0f]);
        factory.Store.Seed(TestMemories.Create("squeezr fact", tome: "alpha") with { ProjectId = "squeezr" }, [1f, 0f]);
        factory.Store.Seed(TestMemories.Create("second overmind fact", tome: "alpha") with { ProjectId = "overmind" }, [1f, 0f]);
        factory.Store.Seed(TestMemories.Create("cross fact", tome: "alpha"), [1f, 0f]);
        factory.Store.Seed(
            TestMemories.Create(
                "buried overmind fact",
                tome: "alpha",
                status: MemoryStatus.Tombstoned,
                tombstoneReason: "gone",
                tombstonedAt: FakeClock.DefaultStart) with { ProjectId = "overmind" },
            [1f, 0f]);

        var response = await factory.NewClient().GetAsync("/api/stats?tome=alpha");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var byProject = body.GetProperty("byProject");
        byProject.GetProperty("overmind").GetInt32().ShouldBe(2);
        byProject.GetProperty("squeezr").GetInt32().ShouldBe(1);
        byProject.GetProperty("cross-project").GetInt32().ShouldBe(1);
        byProject.EnumerateObject().Select(entry => entry.Value.GetInt32()).Sum().ShouldBe(4);
        body.GetProperty("byTome").GetProperty("alpha").GetProperty("active").GetInt64().ShouldBe(4);
    }

    private static CorrectionEntry Patch(DateTimeOffset at, string reason) => new()
    {
        At = at,
        Actor = TestMemories.Agent,
        Action = CorrectionAction.Patch,
        Reason = reason,
    };

    [Fact]
    public async Task Get_CountsActiveIdempotencyDuplicates()
    {
        factory.Reset();
        foreach (var content in new[] { "crashed write a", "crashed write b" })
        {
            factory.Store.Seed(
                TestMemories.Create(content, scope: MemoryScope.Global) with { IdempotencyKey = "shared-key" },
                [1f, 0f]);
        }

        var response = await factory.NewClient().GetAsync("/api/stats");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("idempotencyConflicts").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Get_ByDefault_OmitsPerTomeCompactionAndKeepsTheShape()
    {
        factory.Reset();
        var stale = FakeClock.DefaultStart.AddDays(-30);
        factory.Store.Seed("stale one", [1f, 0f, 0f], createdAt: stale);
        factory.Store.Seed("stale two", [0.97f, 0.24f, 0f], createdAt: stale);

        var response = await factory.NewClient().GetAsync("/api/stats");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("pendingCompaction").GetInt64().ShouldBe(2);
        body.TryGetProperty("pendingCompactionByTome", out _).ShouldBeFalse();
        body.EnumerateObject().Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal)
            .ShouldBe(["active", "activeByScope", "byProject", "byTome", "consolidation", "gc", "idempotencyConflicts", "pendingCompaction", "superseded", "tombstoned", "tome", "tomeSource"]);
    }

    [Fact]
    public async Task Get_WithIncludeTomeCompaction_ReturnsPerTomeBacklog()
    {
        factory.Reset();
        var stale = FakeClock.DefaultStart.AddDays(-30);
        factory.Store.Seed("alpha stale one", [1f, 0f, 0f], createdAt: stale, tome: "alpha");
        factory.Store.Seed("alpha stale two", [0.97f, 0.24f, 0f], createdAt: stale, tome: "alpha");
        factory.Store.Seed("home stale one", [1f, 0f, 0f], createdAt: stale);
        factory.Store.Seed("home stale two", [0.97f, 0.24f, 0f], createdAt: stale);
        factory.Store.Seed("gamma fresh", [0f, 1f, 0f], tome: "gamma");

        var response = await factory.NewClient().GetAsync("/api/stats?includeTomeCompaction=true");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var pending = body.GetProperty("pendingCompactionByTome");
        pending.GetProperty("alpha").GetInt64().ShouldBe(2);
        pending.GetProperty("default").GetInt64().ShouldBe(2);
        pending.GetProperty("gamma").GetInt64().ShouldBe(0);
    }

}
