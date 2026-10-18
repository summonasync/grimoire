using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    public async Task Patch_ContentChange_ReEmbedsAndUpsertsSameId()
    {
        factory.Reset();
        var record = factory.Store.Seed("old note about indexing", [1f, 0f]);
        factory.Embeddings.Vectors["new note about indexing"] = [0.9f, 0.1f];

        var response = await factory.NewClient().PatchAsJsonAsync($"/api/memories/{record.Id}", new
        {
            content = "new note about indexing",
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

        var response = await factory.NewClient().PatchAsJsonAsync($"/api/memories/{record.Id}", new { content = "identical content" });

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

        var response = await factory.NewClient().PatchAsJsonAsync($"/api/memories/{Guid.NewGuid()}", new { content = "x" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_Tombstoned_Returns409()
    {
        factory.Reset();
        var record = factory.Store.Seed("gone", [1f, 0f], status: MemoryStatus.Tombstoned, tombstoneReason: "gone");

        var response = await factory.NewClient().PatchAsJsonAsync($"/api/memories/{record.Id}", new { content = "revive" });

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

    private static async Task<IReadOnlyList<MemoryRecord>> GetRecords(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var records = await ReadFromJsonAsync<MemoryRecord[]>(response);
        return records!;
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
