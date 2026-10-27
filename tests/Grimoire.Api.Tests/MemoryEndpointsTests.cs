using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grimoire.Api.Contracts;
using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class MemoryEndpointsTests(GrimoireApiFactory factory) : IClassFixture<GrimoireApiFactory>
{
    [Fact]
    public async Task List_DefaultShowsActiveOnly()
    {
        factory.Reset();
        SeedStatusSpread();

        var records = await GetRecords(factory.NewClient(), "/api/memories");

        records.ShouldNotBeEmpty();
        records.All(record => record.Status == MemoryStatus.Active).ShouldBeTrue();
    }

    [Fact]
    public async Task List_WithTombstonedStatus_ShowsTombstones()
    {
        factory.Reset();
        SeedStatusSpread();

        var records = await GetRecords(factory.NewClient(), "/api/memories?status=tombstoned");

        records.ShouldNotBeEmpty();
        records.All(record => record.Status == MemoryStatus.Tombstoned).ShouldBeTrue();
    }

    [Fact]
    public async Task List_WithStatusAll_ShowsEveryStatus()
    {
        factory.Reset();
        SeedStatusSpread();

        var records = await GetRecords(factory.NewClient(), "/api/memories?status=all");

        records.Select(record => record.Status).ShouldBe(new[]
        {
            MemoryStatus.Active,
            MemoryStatus.Superseded,
            MemoryStatus.Tombstoned,
        }, ignoreOrder: true);
    }

    [Fact]
    public async Task List_WithMultiStatus_ShowsUnion()
    {
        factory.Reset();
        SeedStatusSpread();

        var records = await GetRecords(factory.NewClient(), "/api/memories?status=superseded,active");

        records.Select(record => record.Status).ShouldBe(new[] { MemoryStatus.Active, MemoryStatus.Superseded }, ignoreOrder: true);
    }

    [Fact]
    public async Task List_WithTomeParam_FiltersAndEchoesTheTome()
    {
        factory.Reset();
        factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");
        factory.Store.Seed("home note", [1f, 0f]);

        var response = await factory.NewClient().GetAsync("/api/memories?tome=alpha");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await ReadList(response);
        list.Tome.ShouldBe("alpha");
        list.Count.ShouldBe(1);
        list.Items.Single().Content.ShouldBe("alpha note");
    }

    [Fact]
    public async Task List_WithoutTomeParam_DefaultsToTheResolvedTome()
    {
        using var tomeFactory = GrimoireApiFactory.Create(envTome: "eng");
        tomeFactory.Store.Seed("eng note", [1f, 0f], tome: "eng");
        tomeFactory.Store.Seed("other tome note", [1f, 0f], tome: "alpha");

        var response = await tomeFactory.NewClient().GetAsync("/api/memories");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await ReadList(response);
        list.Tome.ShouldBe("eng");
        list.Items.Select(record => record.Content).ShouldBe(["eng note"]);
    }

    [Theory]
    [InlineData("Not Valid!!")]
    [InlineData("___")]
    public async Task List_WithGarbageTome_Returns400(string tome)
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync($"/api/memories?tome={Uri.EscapeDataString(tome)}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_tome");
    }

    [Fact]
    public async Task List_WithQuery_HonorsTome()
    {
        factory.Reset();
        factory.Embeddings.Vectors["vector"] = [1f, 0f];
        factory.Store.Seed("alpha vector note", [1f, 0f], tome: "alpha", kind: MemoryKind.Fact);
        factory.Store.Seed("home vector note", [1f, 0f], kind: MemoryKind.Fact);

        var list = await ReadList(await factory.NewClient().GetAsync("/api/memories?query=vector&tome=alpha"));

        list.Tome.ShouldBe("alpha");
        list.Items.Select(record => record.Content).ShouldBe(["alpha vector note"]);
    }

    [Theory]
    [InlineData("zombie", "invalid_status")]
    [InlineData("active;gone", "invalid_status")]
    public async Task List_WithUnknownStatus_Returns400(string status, string expectedCode)
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync($"/api/memories?status={Uri.EscapeDataString(status)}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await ReadError(response);
        error.code.ShouldBe(expectedCode);
    }

    [Fact]
    public async Task List_WithUnknownScope_Returns400()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync("/api/memories?scope=galaxy");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_scope");
    }

    [Fact]
    public async Task List_FiltersByTagAndScope()
    {
        factory.Reset();
        factory.Store.Seed("project note", [1f, 0f], tags: ["qdrant"]);
        factory.Store.Seed("global note", [1f, 0f], scope: MemoryScope.Global, tags: ["qdrant"]);
        factory.Store.Seed("tagged out", [1f, 0f], tags: ["ollama"]);

        var tagged = await GetRecords(factory.NewClient(), "/api/memories?tag=qdrant");
        var scoped = await GetRecords(factory.NewClient(), "/api/memories?scope=global");

        tagged.Count.ShouldBe(2);
        scoped.Select(record => record.Content).ShouldBe(["global note"]);
    }

    [Fact]
    public async Task List_OrdersByCreatedAtDescendingAndAppliesLimit()
    {
        factory.Reset();
        factory.Store.Seed("oldest", [1f, 0f], createdAt: FakeClock.DefaultStart.AddHours(-3));
        factory.Store.Seed("middle", [1f, 0f], createdAt: FakeClock.DefaultStart.AddHours(-2));
        factory.Store.Seed("newest", [1f, 0f], createdAt: FakeClock.DefaultStart.AddHours(-1));

        var limited = await GetRecords(factory.NewClient(), "/api/memories?limit=2");
        var full = await GetRecords(factory.NewClient(), "/api/memories");

        limited.Select(record => record.Content).ShouldBe(["newest", "middle"]);
        full.Select(record => record.Content).ShouldBe(["newest", "middle", "oldest"]);
    }

    [Fact]
    public async Task List_SinceUntilAndActor_ProjectThrough()
    {
        factory.Reset();
        var agentNote = factory.Store.Seed("fresh agent note", [1f, 0f], createdAt: FakeClock.DefaultStart);
        var humanNote = factory.Store.Seed(
            "stale human note",
            [1f, 0f],
            createdAt: FakeClock.DefaultStart.AddDays(-2),
            actor: MemoryActor.Human);

        var inWindow = await GetRecords(factory.NewClient(), "/api/memories?since=2026-10-17T00:00:00Z");
        var beforeWindow = await GetRecords(factory.NewClient(), "/api/memories?until=2026-10-16T00:00:00Z");
        var byAgent = await GetRecords(factory.NewClient(), "/api/memories?actor=dotnet-coder");
        var byHuman = await GetRecords(factory.NewClient(), "/api/memories?actor=human");
        var combined = await GetRecords(factory.NewClient(), "/api/memories?since=2026-10-15T00:00:00Z&actor=human");

        inWindow.Select(record => record.Id).ShouldBe([agentNote.Id]);
        beforeWindow.Select(record => record.Id).ShouldBe([humanNote.Id]);
        byAgent.Select(record => record.Id).ShouldBe([agentNote.Id]);
        byHuman.Select(record => record.Id).ShouldBe([humanNote.Id]);
        combined.Select(record => record.Id).ShouldBe([humanNote.Id]);
    }

    [Fact]
    public async Task List_WithGarbageSince_Returns400NamingTheParameter()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync("/api/memories?since=yesterday");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await ReadError(response);
        error.code.ShouldBe("invalid_since");
        error.message.ShouldContain("'since'");
        error.message.ShouldContain("yesterday");
    }

    [Fact]
    public async Task List_WithGarbageUntil_Returns400NamingTheParameter()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync("/api/memories?until=later");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await ReadError(response);
        error.code.ShouldBe("invalid_until");
        error.message.ShouldContain("'until'");
        error.message.ShouldContain("later");
    }

    [Fact]
    public async Task List_WithSinceAfterUntil_Returns400()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync("/api/memories?since=2026-10-20&until=2026-10-19");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var (code, message) = await ReadError(response);
        code.ShouldBe("invalid_since");
        message.ShouldContain("must not be later than 'until'");
    }

    [Fact]
    public async Task List_WithQuery_RefillsTheWindowWhenActorFiltersWashOutThePage()
    {
        factory.Reset();
        factory.Embeddings.Vectors["vector"] = [1f, 0f];
        factory.Store.Seed("decoy 1", [0.9950f, 0.0998f], actor: MemoryActor.Human);
        factory.Store.Seed("decoy 2", [0.9899f, 0.1421f], actor: MemoryActor.Human);
        factory.Store.Seed("decoy 3", [0.9848f, 0.1736f], actor: MemoryActor.Human);
        factory.Store.Seed("decoy 4", [0.9798f, 0.2000f], actor: MemoryActor.Human);
        factory.Store.Seed("decoy 5", [0.9747f, 0.2234f], actor: MemoryActor.Human);
        factory.Store.Seed("decoy 6", [0.9696f, 0.2449f], actor: MemoryActor.Human);
        var first = factory.Store.Seed("deep one", [0.8660f, 0.5f]);
        var second = factory.Store.Seed("deep two", [0.4f, 0.9165f]);

        var records = await GetRecords(factory.NewClient(), "/api/memories?query=vector&limit=2&actor=dotnet-coder");

        records.Select(record => record.Id).ShouldBe([first.Id, second.Id]);
    }

    [Theory]
    [InlineData("since", true)]
    [InlineData("until", false)]
    public async Task List_BoundaryInstant_IsInclusiveForSinceAndExclusiveForUntil(string param, bool kept)
    {
        factory.Reset();
        var record = factory.Store.Seed("boundary note", [1f, 0f], createdAt: FakeClock.DefaultStart);

        var records = await GetRecords(factory.NewClient(), $"/api/memories?{param}=2026-10-17T12:00:00Z");

        if (kept)
        {
            records.Select(stored => stored.Id).ShouldBe([record.Id]);
        }
        else
        {
            records.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task List_WithBlankNewParams_MatchesTheAbsentParamResponseByteForByte()
    {
        factory.Reset();
        factory.Store.Seed("stable note", [1f, 0f]);

        var absent = await factory.NewClient().GetAsync("/api/memories");
        var blank = await factory.NewClient().GetAsync("/api/memories?since=&until=&actor=");

        absent.StatusCode.ShouldBe(HttpStatusCode.OK);
        blank.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await absent.Content.ReadAsStringAsync()).ShouldBe(await blank.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task List_WithQuery_RanksByEmbeddingSimilarity()
    {
        factory.Reset();
        factory.Embeddings.Vectors["qdrant upserts points"] = [1f, 0f];
        factory.Embeddings.Vectors["sourdough starter notes"] = [0f, 1f];
        factory.Embeddings.Vectors["vector storage"] = [1f, 0f];
        factory.Store.Seed("qdrant upserts points", [1f, 0f]);
        factory.Store.Seed("sourdough starter notes", [0f, 1f]);

        var records = await GetRecords(factory.NewClient(), "/api/memories?query=vector%20storage");

        records.ShouldNotBeEmpty();
        records[0].Content.ShouldBe("qdrant upserts points");
        factory.Embeddings.Requests.ShouldContain("vector storage");
    }

    [Fact]
    public async Task List_WithQuery_HonorsStatusSelection()
    {
        factory.Reset();
        factory.Embeddings.Vectors["active vector note"] = [1f, 0f];
        factory.Embeddings.Vectors["tombstoned vector note"] = [1f, 0f];
        factory.Embeddings.Vectors["vector"] = [1f, 0f];
        factory.Store.Seed("active vector note", [1f, 0f]);
        factory.Store.Seed(
            "tombstoned vector note",
            [1f, 0f],
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "x",
            tombstonedAt: FakeClock.DefaultStart);

        var tombstonedOnly = await GetRecords(factory.NewClient(), "/api/memories?query=vector&status=tombstoned");
        var everyStatus = await GetRecords(factory.NewClient(), "/api/memories?query=vector&status=all");

        tombstonedOnly.Select(record => record.Status).ShouldBe([MemoryStatus.Tombstoned]);
        everyStatus.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Get_Existing_ReturnsRecordAcrossStatuses()
    {
        factory.Reset();
        var tombstoned = factory.Store.Seed("removed note", [1f, 0f], status: MemoryStatus.Tombstoned, tombstoneReason: "wrong");

        var response = await factory.NewClient().GetAsync($"/api/memories/{tombstoned.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var record = await ReadFromJsonAsync<MemoryRecord>(response);
        record!.Id.ShouldBe(tombstoned.Id);
        record.Status.ShouldBe(MemoryStatus.Tombstoned);
        record.TombstoneReason.ShouldBe("wrong");
    }

    [Fact]
    public async Task Get_Unknown_Returns404()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync($"/api/memories/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadError(response)).code.ShouldBe("not_found");
    }

    [Fact]
    public async Task Post_CreatesHumanMemory()
    {
        factory.Reset();
        factory.Embeddings.Vectors["prefers karake over grype"] = [1f, 0f];

        var response = await factory.NewClient().PostAsJsonAsync("/api/memories", new
        {
            content = "prefers karake over grype",
            scope = "personal",
            kind = "fact",
            tags = new[] { "tooling" },
            importance = 0.8,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        var body = await ReadFromJsonAsync<JsonElement>(response);
        body.GetProperty("record").GetProperty("actor").GetProperty("alias").GetString().ShouldBe("human");
        body.GetProperty("record").GetProperty("scope").GetString().ShouldBe("personal");

        var stored = factory.Store.Records.Single();
        stored.Actor.IsHuman.ShouldBeTrue();
        stored.Content.ShouldBe("prefers karake over grype");
        response.Headers.Location!.OriginalString.ShouldBe($"/api/memories/{stored.Id}");
    }

    [Fact]
    public async Task Post_LandsInTheTomeResolvedFromEnvironment()
    {
        using var tomeFactory = GrimoireApiFactory.Create(envTome: "eng");
        tomeFactory.Reset();
        tomeFactory.Embeddings.Vectors["prefers wingman over squad"] = [1f, 0f];

        var response = await tomeFactory.NewClient().PostAsJsonAsync("/api/memories", new
        {
            content = "prefers wingman over squad",
            scope = "global",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await ReadFromJsonAsync<JsonElement>(response);
        body.GetProperty("tome").GetString().ShouldBe("eng");
        tomeFactory.Store.Records.Single().Tome.ShouldBe("eng");
    }

    [Fact]
    public async Task Post_WithExplicitTome_NormalizesAndLandsThere()
    {
        factory.Reset();
        factory.Embeddings.Vectors["claude ships on fridays"] = [1f, 0f];

        var response = await factory.NewClient().PostAsJsonAsync("/api/memories", new
        {
            content = "claude ships on fridays",
            scope = "global",
            tome = "Alpha Prime",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await ReadFromJsonAsync<JsonElement>(response);
        body.GetProperty("tome").GetString().ShouldBe("alpha-prime");
        factory.Store.Records.Single().Tome.ShouldBe("alpha-prime");
    }

    [Fact]
    public async Task Post_WithGarbageTome_Returns400()
    {
        factory.Reset();

        var response = await factory.NewClient().PostAsJsonAsync("/api/memories", new
        {
            content = "anywhere but here",
            scope = "global",
            tome = "Not Valid!!",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_tome");
        factory.Store.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_WithBlankContent_Returns400()
    {
        factory.Reset();

        var response = await factory.NewClient().PostAsJsonAsync("/api/memories", new { content = "   ", scope = "project" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_memory");
    }

    [Fact]
    public async Task Post_WithTypedUpstreamFailure_Returns503WithoutRawDiagnostics()
    {
        factory.Reset();
        factory.Embeddings.Failure = new UpstreamUnavailableException("ollama", new HttpRequestException("connection refused"));

        var response = await factory.NewClient().PostAsJsonAsync("/api/memories", new { content = "offline note", scope = "global" });

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var (code, message) = await ReadError(response);
        code.ShouldBe("upstream_unavailable");
        message.ShouldContain("ollama");
        message.ShouldContain("Nothing was written");
        message.ShouldNotContain("connection refused");
        factory.Store.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_IdenticalRetry_ReportsIdempotentReplay()
    {
        factory.Reset();
        factory.Embeddings.Vectors["one true decision"] = [1f, 0f];
        var client = factory.NewClient();

        var first = await client.PostAsJsonAsync("/api/memories", new { content = "one true decision", scope = "global" });
        var second = await client.PostAsJsonAsync("/api/memories", new { content = "one true decision", scope = "global" });

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await ReadFromJsonAsync<JsonElement>(second);
        body.GetProperty("idempotentReplay").GetBoolean().ShouldBeTrue();
        body.GetProperty("maintenancePending").GetBoolean().ShouldBeFalse();
        factory.Store.Records.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Post_WithMissingContent_Returns400()
    {
        factory.Reset();

        var response = await factory.NewClient().PostAsJsonAsync("/api/memories", new { scope = "project" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_request");
    }

    [Fact]
    public async Task Post_WithMalformedBody_Returns400()
    {
        factory.Reset();
        using var content = new StringContent("{nope", System.Text.Encoding.UTF8, "application/json");

        var response = await factory.NewClient().PostAsync("/api/memories", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_request");
    }

    [Fact]
    public async Task Post_ProjectWithoutProjectIdAndNoPathMatch_Returns400ProjectIdRequired()
    {
        factory.Reset();

        var response = await factory.NewClient().PostAsJsonAsync("/api/memories", new
        {
            content = "project fact",
            scope = "project",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var (code, message) = await ReadError(response);
        code.ShouldBe("project_id_required");
        message.ShouldContain(GrimoireApiFactory.DefaultCallerPath);
        message.ShouldContain("matches no registered project path");
        message.ShouldContain("project registry is empty");
        message.ShouldNotContain("Allowed projectId values");
        factory.Store.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_UnknownProjectId_Returns400WithTheRegisteredSet()
    {
        using var tagged = GrimoireApiFactory.Create(
            projects:
            [
                new ProjectAliasRecord { Alias = "grimoire", Paths = ["/src/grimoire"] },
                new ProjectAliasRecord { Alias = "alpha", Paths = ["/src/alpha"] },
            ]);
        tagged.Reset();

        var response = await tagged.NewClient().PostAsJsonAsync("/api/memories", new
        {
            content = "project fact",
            scope = "project",
            projectId = "Slurpy.AI",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var (code, message) = await ReadError(response);
        code.ShouldBe("project_id_unknown");
        message.ShouldContain("'Slurpy.AI' is not a registered project alias");
        message.ShouldContain("Allowed projectId values (case-sensitive, pass one exactly as advertised): alpha, grimoire");
        tagged.Store.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_ProjectWithoutProjectId_AutotagsFromTheServerWorkingDirectory()
    {
        using var tagged = GrimoireApiFactory.Create(
            projects: [new ProjectAliasRecord { Alias = "grimoire", Paths = ["/src/grimoire"] }],
            callerPath: "/src/grimoire/sub");
        tagged.Reset();
        tagged.Embeddings.Vectors["project fact"] = [1f, 0f];

        var response = await tagged.NewClient().PostAsJsonAsync("/api/memories", new
        {
            content = "project fact",
            scope = "project",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        tagged.Store.Records.ShouldHaveSingleItem().ProjectId.ShouldBe("grimoire");
    }

    [Theory]
    [InlineData("global")]
    [InlineData("personal")]
    public async Task Post_LongTermScopeWithoutProjectId_WritesWithoutTag(string scope)
    {
        factory.Reset();
        factory.Embeddings.Vectors["cross project fact"] = [1f, 0f];

        var response = await factory.NewClient().PostAsJsonAsync("/api/memories", new
        {
            content = "cross project fact",
            scope,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        factory.Store.Records.ShouldHaveSingleItem().ProjectId.ShouldBeNull();
    }

    [Theory]
    [InlineData("global")]
    [InlineData("personal")]
    public async Task Post_LongTermScopeWithUnknownProjectId_Returns400(string scope)
    {
        factory.Reset();

        var response = await factory.NewClient().PostAsJsonAsync("/api/memories", new
        {
            content = "cross project fact",
            scope,
            projectId = "Slurpy.AI",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("project_id_unknown");
        factory.Store.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_SessionScopeWithProjectId_Returns400ParamConflict()
    {
        factory.Reset();

        var response = await factory.NewClient().PostAsJsonAsync("/api/memories", new
        {
            content = "session note",
            scope = "session",
            projectId = "grimoire",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var (code, message) = await ReadError(response);
        code.ShouldBe("param_conflict");
        message.ShouldContain("projectId only applies to scopes 'project', 'global' and 'personal'");
        message.ShouldContain("(got projectId 'grimoire' with scope 'session')");
        factory.Store.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_SessionScopeWithoutProjectId_WritesSessionRecordWithoutTag()
    {
        factory.Reset();
        factory.Embeddings.Vectors["session note"] = [1f, 0f];

        var response = await factory.NewClient().PostAsJsonAsync("/api/memories", new
        {
            content = "session note",
            scope = "session",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var record = factory.Store.Records.ShouldHaveSingleItem();
        record.Scope.ShouldBe(MemoryScope.Session);
        record.ProjectId.ShouldBeNull();
        record.SessionId.ShouldBeNull();
    }

    [Theory]
    [InlineData("project")]
    [InlineData("global")]
    [InlineData("personal")]
    public async Task Post_NonSessionScopeWithSessionId_Returns400ParamConflict(string scope)
    {
        factory.Reset();

        var response = await factory.NewClient().PostAsJsonAsync("/api/memories", new
        {
            content = "long term note",
            scope,
            sessionId = "s1",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var (code, message) = await ReadError(response);
        code.ShouldBe("param_conflict");
        message.ShouldContain("sessionId only applies to scope 'session'");
        message.ShouldContain($"(got sessionId 's1' with scope '{scope}')");
        factory.Store.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task Patch_ContentChange_ReEmbedsAndUpsertsSameId()
    {
        factory.Reset();
        var record = factory.Store.Seed("old note about indexing", [1f, 0f]);
        factory.Embeddings.Vectors["new note about indexing"] = [0.9f, 0.1f];

        var response = await factory.NewClient().PatchAsJsonAsync($"/api/memories/{record.Id}", new
        {
            content = "new note about indexing",
            reason = "reword",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ReadFromJsonAsync<MemoryRecord>(response);
        updated!.Id.ShouldBe(record.Id);
        updated.Content.ShouldBe("new note about indexing");

        factory.Embeddings.Requests.ShouldContain("new note about indexing");
        factory.Store.Vectors[record.Id].ShouldBe([0.9f, 0.1f]);
        factory.Store.UpsertCount.ShouldBe(1);
        factory.Store.UpdateCount.ShouldBe(0);
    }

    [Fact]
    public async Task Patch_WithoutContentChange_UpdatesWithoutEmbedding()
    {
        factory.Reset();
        var record = factory.Store.Seed("stable note", [1f, 0f], tags: ["before"], importance: 0.4);

        var response = await factory.NewClient().PatchAsJsonAsync($"/api/memories/{record.Id}", new
        {
            tags = new[] { "after" },
            importance = 1.5,
            reason = "retagged",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ReadFromJsonAsync<MemoryRecord>(response);
        updated!.Tags.ShouldBe(["after"]);
        updated.Importance.ShouldBe(1);
        updated.Content.ShouldBe("stable note");

        factory.Embeddings.Requests.ShouldBeEmpty();
        factory.Store.UpsertCount.ShouldBe(0);
        factory.Store.UpdateCount.ShouldBe(1);
    }

    [Fact]
    public async Task Patch_WithSameContent_SkipsReEmbed()
    {
        factory.Reset();
        var record = factory.Store.Seed("identical content", [1f, 0f]);

        var response = await factory.NewClient().PatchAsJsonAsync($"/api/memories/{record.Id}", new { content = "identical content", reason = "no-op touch" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        factory.Embeddings.Requests.ShouldBeEmpty();
        factory.Store.UpsertCount.ShouldBe(0);
        factory.Store.UpdateCount.ShouldBe(1);
    }

    [Fact]
    public async Task Patch_DoesNotCarryReasonIntoRecord()
    {
        factory.Reset();
        var record = factory.Store.Seed("typo in fact", [1f, 0f]);
        factory.Embeddings.Vectors["fixed fact"] = [1f, 0f];

        var response = await factory.NewClient().PatchAsJsonAsync($"/api/memories/{record.Id}", new
        {
            content = "fixed fact",
            reason = "typo fix",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ReadFromJsonAsync<MemoryRecord>(response);
        updated!.TombstoneReason.ShouldBeNull();
        updated.TombstonedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Patch_Unknown_Returns404()
    {
        factory.Reset();

        var response = await factory.NewClient().PatchAsJsonAsync($"/api/memories/{Guid.NewGuid()}", new { content = "x", reason = "why" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_Tombstoned_Returns409()
    {
        factory.Reset();
        var record = factory.Store.Seed("gone", [1f, 0f], status: MemoryStatus.Tombstoned, tombstoneReason: "gone");

        var response = await factory.NewClient().PatchAsJsonAsync($"/api/memories/{record.Id}", new { content = "revive", reason = "revive attempt" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadError(response)).code.ShouldBe("not_editable");
    }

    [Fact]
    public async Task Patch_WithEmptyBody_Returns400()
    {
        factory.Reset();
        var record = factory.Store.Seed("note", [1f, 0f]);

        var response = await factory.NewClient().PatchAsJsonAsync($"/api/memories/{record.Id}", new { reason = "no fields" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_patch");
    }

    [Fact]
    public async Task Patch_WithMalformedId_Returns404()
    {
        factory.Reset();

        var response = await factory.NewClient().PatchAsJsonAsync("/api/memories/not-a-guid", new { content = "x" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_WithoutReason_Returns400()
    {
        factory.Reset();
        var record = factory.Store.Seed("note", [1f, 0f]);

        var missing = await factory.NewClient().PatchAsJsonAsync($"/api/memories/{record.Id}", new { content = "changed" });
        var blank = await factory.NewClient().PatchAsJsonAsync($"/api/memories/{record.Id}", new { content = "changed", reason = "   " });

        missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        blank.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(missing)).code.ShouldBe("invalid_reason");
        (await ReadError(blank)).code.ShouldBe("invalid_reason");
        factory.Store.Records.Single().Content.ShouldBe("note");
        factory.Store.Records.Single().Corrections.ShouldBe([]);
    }

    [Fact]
    public async Task Patch_WithReason_AppendsTrailLineVisibleOnDetail()
    {
        factory.Reset();
        var record = factory.Store.Seed("old fact", [1f, 0f]);
        factory.Clock.Advance(TimeSpan.FromMinutes(5));
        factory.Embeddings.Vectors["new fact"] = [0.9f, 0.1f];

        var response = await factory.NewClient().PatchAsJsonAsync($"/api/memories/{record.Id}", new
        {
            content = "new fact",
            reason = "the value moved",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var patched = await ReadFromJsonAsync<MemoryRecord>(response);
        var line = patched!.Corrections.Single();
        line.Action.ShouldBe(CorrectionAction.Patch);
        line.Reason.ShouldBe("the value moved");
        line.Actor.ShouldBe(MemoryActor.Human);
        line.At.ShouldBe(factory.Clock.UtcNow);

        var detail = await ReadFromJsonAsync<MemoryRecord>(
            await factory.NewClient().GetAsync($"/api/memories/{record.Id}"));
        detail!.Corrections.Select(correction => correction.Action).ShouldBe([CorrectionAction.Patch]);
        factory.Store.Vectors[record.Id].ShouldBe([0.9f, 0.1f]);
    }

    [Fact]
    public async Task Forget_TombstonesWithReasonAndTimestamp()
    {
        factory.Reset();
        var record = factory.Store.Seed("to be forgotten", [1f, 0f]);

        var response = await factory.NewClient().PostAsJsonAsync($"/api/memories/{record.Id}/forget", new { reason = "superseded by curated fact" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ReadFromJsonAsync<MemoryRecord>(response);
        updated!.Status.ShouldBe(MemoryStatus.Tombstoned);
        updated.TombstoneReason.ShouldBe("superseded by curated fact");
        updated.TombstonedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Forget_WithoutReason_Returns400()
    {
        factory.Reset();
        var record = factory.Store.Seed("note", [1f, 0f]);

        var missing = await factory.NewClient().PostAsJsonAsync($"/api/memories/{record.Id}/forget", new { });
        var blank = await factory.NewClient().PostAsJsonAsync($"/api/memories/{record.Id}/forget", new { reason = "  " });

        missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        blank.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(blank)).code.ShouldBe("invalid_reason");
        factory.Store.Records.Single().Status.ShouldBe(MemoryStatus.Active);
    }

    [Fact]
    public async Task Forget_Unknown_Returns404()
    {
        factory.Reset();

        var response = await factory.NewClient().PostAsJsonAsync($"/api/memories/{Guid.NewGuid()}/forget", new { reason = "anything" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Forget_AlreadyTombstoned_Returns409()
    {
        factory.Reset();
        var record = factory.Store.Seed("already gone", [1f, 0f], status: MemoryStatus.Tombstoned, tombstoneReason: "first");

        var response = await factory.NewClient().PostAsJsonAsync($"/api/memories/{record.Id}/forget", new { reason = "second" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadError(response)).code.ShouldBe("already_tombstoned");
    }

    [Fact]
    public async Task Forget_WithTomeParameter_TombstonesWithinThatTome()
    {
        factory.Reset();
        var record = factory.Store.Seed("alpha retraction", [1f, 0f], tome: "alpha");

        var response = await factory.NewClient().PostAsJsonAsync(
            $"/api/memories/{record.Id}/forget",
            new { reason = "retracted", tome = "alpha" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        factory.Store.Records.Single().Status.ShouldBe(MemoryStatus.Tombstoned);
    }

    [Fact]
    public async Task Forget_WithIdFromAnotherTome_Returns404WithoutTouchingIt()
    {
        factory.Reset();
        var record = factory.Store.Seed("alpha retraction", [1f, 0f], tome: "alpha");

        var response = await factory.NewClient().PostAsJsonAsync($"/api/memories/{record.Id}/forget", new { reason = "retracted" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadError(response)).code.ShouldBe("not_found");
        factory.Store.Records.Single().Status.ShouldBe(MemoryStatus.Active);
    }

    [Fact]
    public async Task Forget_WithGarbageTome_Returns400()
    {
        factory.Reset();
        var record = factory.Store.Seed("note", [1f, 0f]);

        var response = await factory.NewClient().PostAsJsonAsync(
            $"/api/memories/{record.Id}/forget",
            new { reason = "retracted", tome = "Not Valid!!" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_tome");
        factory.Store.Records.Single().Status.ShouldBe(MemoryStatus.Active);
    }

    [Fact]
    public async Task Forget_TombstonedByOtherFaceAfterPrecheck_Returns409()
    {
        factory.Reset();
        var record = factory.Store.Seed("raced note", [1f, 0f]);
        RaceOnGet(2, () => factory.Store.Seed(record with
        {
            Status = MemoryStatus.Tombstoned,
            TombstoneReason = "other face",
            TombstonedAt = factory.Clock.UtcNow,
        }, [1f, 0f]));

        var response = await factory.NewClient().PostAsJsonAsync($"/api/memories/{record.Id}/forget", new { reason = "mine" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadError(response)).code.ShouldBe("already_tombstoned");
    }

    [Fact]
    public async Task Forget_DeletedBeforeTombstone_Returns404()
    {
        factory.Reset();
        var record = factory.Store.Seed("raced note", [1f, 0f]);
        RaceOnGet(2, factory.Store.Clear);

        var response = await factory.NewClient().PostAsJsonAsync($"/api/memories/{record.Id}/forget", new { reason = "mine" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadError(response)).code.ShouldBe("not_found");
    }

    [Fact]
    public async Task Forget_DeletedAfterTombstone_Returns404()
    {
        factory.Reset();
        var record = factory.Store.Seed("raced note", [1f, 0f]);
        RaceOnGet(3, factory.Store.Clear);

        var response = await factory.NewClient().PostAsJsonAsync($"/api/memories/{record.Id}/forget", new { reason = "mine" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadError(response)).code.ShouldBe("not_found");
        factory.Store.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task Restore_SupersededRecord_ActivatesOriginalAndReportsSuperseder()
    {
        factory.Reset();
        var successor = factory.Store.Seed("deploy moved to monday", [1f, 0f], createdAt: FakeClock.DefaultStart.AddHours(1));
        var original = factory.Store.Seed(
            "deploy on friday",
            [1f, 0f],
            createdAt: FakeClock.DefaultStart,
            status: MemoryStatus.Superseded);
        factory.Store.Seed(original with { SupersededBy = successor.Id }, [1f, 0f]);
        factory.Clock.Advance(TimeSpan.FromHours(2));

        var response = await factory.NewClient().PostAsJsonAsync($"/api/memories/{original.Id}/restore", new { reason = "friday was right" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadFromJsonAsync<RestoreMemoryResponse>(response);
        body!.Restored.ShouldBeTrue();
        body.Tome.ShouldBe(TomeKey.DefaultTome);
        body.Record.Status.ShouldBe(MemoryStatus.Active);
        body.Superseder.ShouldNotBeNull();
        body.Superseder.Id.ShouldBe(successor.Id);
        body.Superseder.Status.ShouldBe(MemoryStatus.Tombstoned);
        body.Superseder.TombstoneReason.ShouldBe($"restored {original.Id}");
        factory.Store.Records.Single(stored => stored.Status == MemoryStatus.Active).Id.ShouldBe(original.Id);
    }

    [Fact]
    public async Task Restore_TombstonedRecord_ClearsTombstoneAndAppendsTrail()
    {
        factory.Reset();
        var record = factory.Store.Seed(
            "retracted note",
            [1f, 0f],
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "wrong call",
            tombstonedAt: FakeClock.DefaultStart);
        factory.Clock.Advance(TimeSpan.FromHours(1));

        var response = await factory.NewClient().PostAsJsonAsync($"/api/memories/{record.Id}/restore", new { reason = "it was true after all" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadFromJsonAsync<RestoreMemoryResponse>(response);
        body!.Restored.ShouldBeTrue();
        body.Superseder.ShouldBeNull();
        body.Record.TombstoneReason.ShouldBeNull();
        body.Record.TombstonedAt.ShouldBeNull();
        body.Record.Corrections.Single().Action.ShouldBe(CorrectionAction.Restore);
        factory.Store.Records.Single().Status.ShouldBe(MemoryStatus.Active);
    }

    [Fact]
    public async Task Restore_ActiveRecord_IsNoOpReportingRestoredFalse()
    {
        factory.Reset();
        var record = factory.Store.Seed("still true", [1f, 0f]);

        var response = await factory.NewClient().PostAsJsonAsync($"/api/memories/{record.Id}/restore", new { reason = "double check" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadFromJsonAsync<RestoreMemoryResponse>(response);
        body!.Restored.ShouldBeFalse();
        body.Superseder.ShouldBeNull();
        body.Record.Id.ShouldBe(record.Id);
        factory.Store.UpdateCount.ShouldBe(0);
        factory.Store.Records.Single().Corrections.ShouldBe([]);
    }

    [Fact]
    public async Task Restore_Unknown_Returns404()
    {
        factory.Reset();

        var response = await factory.NewClient().PostAsJsonAsync($"/api/memories/{Guid.NewGuid()}/restore", new { reason = "gone" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadError(response)).code.ShouldBe("not_found");
    }

    [Fact]
    public async Task Restore_WithIdFromAnotherTome_Returns404WithoutTouchingIt()
    {
        factory.Reset();
        var record = factory.Store.Seed(
            "alpha note",
            [1f, 0f],
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "gone",
            tombstonedAt: FakeClock.DefaultStart,
            tome: "alpha");

        var response = await factory.NewClient().PostAsJsonAsync($"/api/memories/{record.Id}/restore", new { reason = "bring it back" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadError(response)).code.ShouldBe("not_found");
        factory.Store.Records.Single().Status.ShouldBe(MemoryStatus.Tombstoned);
        factory.Store.Records.Single().TombstoneReason.ShouldBe("gone");
    }

    [Fact]
    public async Task Restore_WithTomeParameter_RestoresWithinThatTome()
    {
        factory.Reset();
        var record = factory.Store.Seed(
            "alpha retraction",
            [1f, 0f],
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "gone",
            tombstonedAt: FakeClock.DefaultStart,
            tome: "alpha");

        var response = await factory.NewClient().PostAsJsonAsync($"/api/memories/{record.Id}/restore", new { reason = "undone", tome = "alpha" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadFromJsonAsync<RestoreMemoryResponse>(response);
        body!.Tome.ShouldBe("alpha");
        body.Restored.ShouldBeTrue();
        body.Record.Status.ShouldBe(MemoryStatus.Active);
    }

    [Fact]
    public async Task Restore_WithoutReason_Returns400()
    {
        factory.Reset();
        var record = factory.Store.Seed("note", [1f, 0f]);

        var blank = await factory.NewClient().PostAsJsonAsync($"/api/memories/{record.Id}/restore", new { reason = "  " });
        var missing = await factory.NewClient().PostAsJsonAsync($"/api/memories/{record.Id}/restore", new { });

        blank.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(blank)).code.ShouldBe("invalid_reason");
        (await ReadError(missing)).code.ShouldBe("invalid_request");
        factory.Store.Records.Single().Status.ShouldBe(MemoryStatus.Active);
    }

    [Fact]
    public async Task Restore_WithGarbageTome_Returns400()
    {
        factory.Reset();
        var record = factory.Store.Seed("note", [1f, 0f]);

        var response = await factory.NewClient().PostAsJsonAsync($"/api/memories/{record.Id}/restore", new { reason = "undone", tome = "Not Valid!!" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_tome");
        factory.Store.Records.Single().Status.ShouldBe(MemoryStatus.Active);
    }

    [Fact]
    public async Task History_ReturnsFullChainOldestFirst()
    {
        factory.Reset();
        var successor = factory.Store.Seed("deploy moved to monday", [1f, 0f], createdAt: FakeClock.DefaultStart.AddHours(-1));
        var original = factory.Store.Seed(
            "deploy on friday",
            [1f, 0f],
            createdAt: FakeClock.DefaultStart.AddHours(-2),
            status: MemoryStatus.Superseded);
        factory.Store.Seed(original with { SupersededBy = successor.Id }, [1f, 0f]);

        var fromOriginal = await ReadFromJsonAsync<HistoryResponse>(
            await factory.NewClient().GetAsync($"/api/memories/{original.Id}/history"));
        var fromSuccessor = await ReadFromJsonAsync<HistoryResponse>(
            await factory.NewClient().GetAsync($"/api/memories/{successor.Id}/history"));

        fromOriginal!.Tome.ShouldBe(TomeKey.DefaultTome);
        fromOriginal.Chain.Select(entry => entry.Id).ShouldBe([original.Id, successor.Id]);
        fromOriginal.Chain[0].Status.ShouldBe(MemoryStatus.Superseded);
        fromOriginal.Chain[0].BodyPreview.ShouldBe("deploy on friday");
        fromOriginal.Chain[1].Status.ShouldBe(MemoryStatus.Active);
        fromOriginal.Chain[1].BodyPreview.ShouldBe("deploy moved to monday");
        fromSuccessor!.Chain.Select(entry => entry.Id).ShouldBe([original.Id, successor.Id]);
    }

    [Fact]
    public async Task History_Unknown_Returns404()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync($"/api/memories/{Guid.NewGuid()}/history");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadError(response)).code.ShouldBe("not_found");
    }

    [Fact]
    public async Task History_WithIdFromAnotherTome_Returns404()
    {
        factory.Reset();
        var record = factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");

        var response = await factory.NewClient().GetAsync($"/api/memories/{record.Id}/history");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadError(response)).code.ShouldBe("not_found");
    }

    [Fact]
    public async Task History_WithTomeParameter_FollowsThatTome()
    {
        factory.Reset();
        var record = factory.Store.Seed("alpha note", [1f, 0f], tome: "alpha");

        var response = await factory.NewClient().GetAsync($"/api/memories/{record.Id}/history?tome=alpha");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var history = await ReadFromJsonAsync<HistoryResponse>(response);
        history!.Tome.ShouldBe("alpha");
        history.Chain.Select(entry => entry.Id).ShouldBe([record.Id]);
    }

    [Fact]
    public async Task History_WithGarbageTome_Returns400()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync($"/api/memories/{Guid.NewGuid()}/history?tome={Uri.EscapeDataString("Not Valid!!")}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_tome");
    }

    [Fact]
    public async Task List_WithFields_ProjectsEachItemToIdPlusTheRequestedKeys()
    {
        factory.Reset();
        factory.Store.Seed("projected note", [1f, 0f], tags: ["ops-crew"]);

        var body = await ReadFromJsonAsync<JsonElement>(await factory.NewClient().GetAsync("/api/memories?fields=content,createdAt"));

        body.EnumerateObject().Select(property => property.Name).ShouldBe(["tome", "count", "items"]);
        var item = body.GetProperty("items")[0];
        item.EnumerateObject().Select(property => property.Name).ShouldBe(["id", "content", "createdAt"]);
        item.GetProperty("content").GetString().ShouldBe("projected note");
        item.GetProperty("createdAt").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task List_WithExplicitIdField_KeepsIdSingle()
    {
        factory.Reset();
        factory.Store.Seed("projected note", [1f, 0f]);

        var body = await ReadFromJsonAsync<JsonElement>(await factory.NewClient().GetAsync("/api/memories?fields=id,content"));

        body.GetProperty("items")[0].EnumerateObject().Select(property => property.Name)
            .ShouldBe(["id", "content"]);
    }

    [Fact]
    public async Task List_WithUnknownField_Returns400NamingTheAllowedSet()
    {
        factory.Reset();
        factory.Store.Seed("projected note", [1f, 0f]);

        var response = await factory.NewClient().GetAsync("/api/memories?fields=content,psychicVibes");
        var (code, message) = await ReadError(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        code.ShouldBe("invalid_fields");
        message.ShouldContain("psychicVibes");
        message.ShouldContain("Allowed fields:");
        message.ShouldContain("tombstoneReason");
    }

    [Theory]
    [InlineData("content,,createdAt")]
    [InlineData("content,%20,createdAt")]
    [InlineData(",")]
    public async Task List_WithEmptyFieldEntries_Returns400(string fields)
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync($"/api/memories?fields={fields}");
        var (code, _) = await ReadError(response);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        code.ShouldBe("invalid_fields");
    }

    [Fact]
    public async Task List_WithoutFields_ReturnsTheUnchangedFullRecordShape()
    {
        factory.Reset();
        factory.Store.Seed("full shape note", [1f, 0f], tags: ["ops-crew"]);

        var body = await ReadFromJsonAsync<JsonElement>(await factory.NewClient().GetAsync("/api/memories"));
        var item = body.GetProperty("items")[0];

        item.EnumerateObject().Select(property => property.Name).ShouldBe(
        [
            "id", "content", "scope", "kind", "actor", "tome", "projectId", "sessionId", "tags",
            "importance", "createdAt", "lastAccessedAt", "accessCount", "expiresAt", "status",
            "supersededBy", "tombstoneReason", "tombstonedAt", "sources", "corrections", "idempotencyKey",
        ]);
    }

    private void RaceOnGet(int atCall, Action mutate)
    {
        factory.Store.OnGetAsync = () =>
        {
            if (factory.Store.GetCount == atCall)
            {
                mutate();
            }
        };
    }

    private void SeedStatusSpread()
    {
        factory.Store.Seed("active note", [1f, 0f]);
        factory.Store.Seed("superseded note", [1f, 0f], status: MemoryStatus.Superseded, supersededBy: Guid.NewGuid());
        factory.Store.Seed(
            "tombstoned note",
            [1f, 0f],
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "wrong",
            tombstonedAt: FakeClock.DefaultStart);
    }

    private static async Task<MemoryListResponse> ReadList(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return (await JsonSerializer.DeserializeAsync<MemoryListResponse>(stream, TestJson.Options))!;
    }

    private static async Task<IReadOnlyList<MemoryRecord>> GetRecords(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var list = await ReadFromJsonAsync<MemoryListResponse>(response);
        return list!.Items;
    }

    private static async Task<T?> ReadFromJsonAsync<T>(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(stream, TestJson.Options);
    }

    private static async Task<(string code, string message)> ReadError(HttpResponseMessage response)
    {
        var body = await ReadFromJsonAsync<JsonElement>(response);
        var error = body.GetProperty("error");
        return (error.GetProperty("code").GetString()!, error.GetProperty("message").GetString()!);
    }
}
