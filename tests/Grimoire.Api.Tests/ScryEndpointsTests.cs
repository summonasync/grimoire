using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class ScryEndpointsTests(GrimoireApiFactory factory) : IClassFixture<GrimoireApiFactory>
{
    [Fact]
    public async Task Scry_ExcludesTheOwnTome_AndSearchesEveryOther()
    {
        factory.Reset();
        Seed("alpha squad decision", tome: "alpha");
        Seed("own tome note");

        var body = await ScryAsync("alpha squad decision");

        body.GetProperty("tomes").EnumerateArray().Select(value => value.GetString()).ShouldBe(["alpha"]);
        body.GetProperty("count").GetInt32().ShouldBe(1);
        var hit = body.GetProperty("items")[0];
        hit.GetProperty("content").GetString().ShouldBe("alpha squad decision");
        hit.GetProperty("tome").GetString().ShouldBe("alpha");
        hit.GetProperty("kind").GetString().ShouldBe("fact");
    }

    [Fact]
    public async Task Scry_ByDefault_IsAnonymous()
    {
        factory.Reset();
        Seed("qwen ships on fridays", tome: "alpha", actor: MemoryActor.Agent("bob", "qwen"));

        var body = await ScryAsync("qwen ships on fridays");

        body.GetProperty("attribution").GetString().ShouldBe("none");
        body.GetProperty("items")[0].TryGetProperty("actor", out _).ShouldBeFalse("scry must be anonymous by default");
    }

    [Fact]
    public async Task Scry_WithNoneAttribution_OmitsTheActorPropertyEntirely()
    {
        factory.Reset();
        Seed("qwen ships on fridays", tome: "alpha", actor: MemoryActor.Agent("bob", "qwen"));

        var body = await ScryAsync("qwen ships on fridays", "attribution=none");

        body.GetProperty("items")[0].TryGetProperty("actor", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Scry_WithLightAttribution_KeepsOnlyTheAlias()
    {
        factory.Reset();
        Seed("qwen ships on fridays", tome: "alpha", actor: MemoryActor.Agent("bob", "qwen"));

        var body = await ScryAsync("qwen ships on fridays", "attribution=light");

        var actor = body.GetProperty("items")[0].GetProperty("actor").GetString().ShouldNotBeNull();
        actor.ShouldBe("bob");
        actor.ShouldNotContain("qwen");
    }

    [Fact]
    public async Task Scry_WithFullAttribution_KeepsAliasAndVendor()
    {
        factory.Reset();
        Seed("qwen ships on fridays", tome: "alpha", actor: MemoryActor.Agent("bob", "qwen"));

        var body = await ScryAsync("qwen ships on fridays", "attribution=full");

        body.GetProperty("items")[0].GetProperty("actor").GetString().ShouldBe("bob+qwen");
    }

    [Fact]
    public async Task Scry_Journal_IsOptIn()
    {
        factory.Reset();
        Seed("wired the rig before noon", tome: "alpha", kind: MemoryKind.Journal);

        var factsOnly = await ScryAsync("wired the rig before noon");
        var withJournal = await ScryAsync("wired the rig before noon", "includeJournal=true");

        factsOnly.GetProperty("count").GetInt32().ShouldBe(0);
        withJournal.GetProperty("count").GetInt32().ShouldBe(1);
        withJournal.GetProperty("items")[0].GetProperty("kind").GetString().ShouldBe("journal");
    }

    [Fact]
    public async Task Scry_SessionMemories_NeverCrossTomes()
    {
        factory.Reset();
        factory.Store.Seed(
            "session scratchpad note",
            [1f, 0f],
            scope: MemoryScope.Session,
            kind: MemoryKind.Journal,
            tome: "alpha",
            expiresAt: FakeClock.DefaultStart.AddHours(2));
        factory.Embeddings.Vectors["session scratchpad note"] = [1f, 0f];

        var body = await ScryAsync("session scratchpad note", "includeJournal=true");

        body.GetProperty("count").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Scry_TombstonedAndSuperseded_NeverSurface()
    {
        factory.Reset();
        factory.Store.Seed(
            "retracted alpha note",
            [1f, 0f],
            tome: "alpha",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "retracted",
            tombstonedAt: FakeClock.DefaultStart);
        factory.Store.Seed("superseded alpha note", [1f, 0f], tome: "alpha", status: MemoryStatus.Superseded, supersededBy: Guid.NewGuid());
        factory.Embeddings.Vectors["alpha note"] = [1f, 0f];

        var body = await ScryAsync("alpha note");

        body.GetProperty("count").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Scry_ExplicitTomes_NarrowTheSearch()
    {
        factory.Reset();
        Seed("alpha team decision", tome: "alpha");
        Seed("beta team decision", tome: "beta");
        factory.Embeddings.Vectors["team decision"] = [1f, 0f];

        var body = await ScryAsync("team decision", "tomes=alpha");

        body.GetProperty("tomes").EnumerateArray().Select(value => value.GetString()).ShouldBe(["alpha"]);
        body.GetProperty("items").EnumerateArray().Select(hit => hit.GetProperty("content").GetString())
            .ShouldBe(["alpha team decision"]);
    }

    [Fact]
    public async Task Scry_WithoutOtherTomes_ReturnsAnHonestEmptyResult()
    {
        factory.Reset();
        Seed("only own tome note");

        var body = await ScryAsync("only own tome note");

        body.GetProperty("tomes").GetArrayLength().ShouldBe(0);
        body.GetProperty("items").GetArrayLength().ShouldBe(0);
        body.GetProperty("count").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Scry_IsNotExposedUnderTheApiPrefix()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync("/api/scry?q=anything");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("Not Valid!!")]
    [InlineData("___")]
    public async Task Scry_WithGarbageTomes_Returns400(string tomes)
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync($"/scry?q=anything&tomes={Uri.EscapeDataString(tomes)}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).ShouldBe("invalid_tome");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Scry_WithBlankQuery_Returns400(string query)
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync($"/scry?q={Uri.EscapeDataString(query)}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).ShouldBe("invalid_query");
    }

    [Fact]
    public async Task Scry_WithoutQuery_Returns400()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync("/scry");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).ShouldBe("invalid_query");
    }

    [Fact]
    public async Task Scry_WithUnknownAttribution_Returns400()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync("/scry?q=anything&attribution=psychic");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).ShouldBe("invalid_attribution");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Scry_WithNonPositiveLimit_Returns400(int limit)
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync($"/scry?q=anything&limit={limit}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).ShouldBe("invalid_limit");
    }

    [Fact]
    public async Task Scry_LimitCapsTheHits()
    {
        factory.Reset();
        Seed("alpha note one", tome: "alpha");
        Seed("alpha note two", tome: "alpha");
        Seed("alpha note three", tome: "alpha");
        factory.Embeddings.Vectors["alpha note"] = [1f, 0f];

        var body = await ScryAsync("alpha note", "limit=1");

        body.GetProperty("count").GetInt32().ShouldBe(1);
    }

    private void Seed(
        string content,
        string tome = TomeKey.DefaultTome,
        MemoryKind kind = MemoryKind.Fact,
        MemoryActor? actor = null)
    {
        factory.Embeddings.Vectors.TryAdd(content, [1f, 0f]);
        factory.Store.Seed(content, [1f, 0f], scope: MemoryScope.Global, kind: kind, actor: actor, tome: tome);
    }

    private async Task<JsonElement> ScryAsync(string query, string extra = "")
    {
        var separator = extra.Length == 0 ? string.Empty : "&";
        using var response = await factory.NewClient().GetAsync($"/scry?q={Uri.EscapeDataString(query)}{separator}{extra}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var stream = await response.Content.ReadAsStreamAsync();
        return (await JsonSerializer.DeserializeAsync<JsonElement>(stream, TestJson.Options))!;
    }

    private static async Task<string> ReadError(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(stream, TestJson.Options);
        return body.GetProperty("error").GetProperty("code").GetString()!;
    }
}
