using System.Net;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class OriginGuardTests
{
    [Theory]
    [InlineData("http://evil.example")]
    [InlineData("http://localhost.evil.example")]
    [InlineData("https://localhost")]
    [InlineData("null")]
    public async Task Patch_WithNonLoopbackOrigin_RefusedAndRecordUnchanged(string origin)
    {
        using var factory = new GrimoireApiFactory();
        var record = factory.Store.Seed("guarded note", [1f, 0f], tags: ["keep"], importance: 0.3);
        factory.Embeddings.Vectors["smuggled edit"] = [0.9f, 0.1f];

        var response = await RawHttpProbe.SendAsync(
            factory.NewClient().BaseAddress!,
            "PATCH",
            $"/api/memories/{record.Id}",
            host: "127.0.0.1",
            origin: origin,
            body: """{"content":"smuggled edit","reason":"rebind attempt"}""");

        response.Status.ShouldBe(HttpStatusCode.Forbidden);
        response.Raw.ShouldContain("forbidden_origin");
        var stored = factory.Store.Records.Single(r => r.Id == record.Id);
        stored.ShouldBe(record);
        stored.Content.ShouldBe("guarded note");
        factory.Store.UpsertCount.ShouldBe(0);
        factory.Store.UpdateCount.ShouldBe(0);
        factory.Store.GetCount.ShouldBe(0);
        factory.Embeddings.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Patch_WithoutOrigin_Succeeds()
    {
        using var factory = new GrimoireApiFactory();
        var record = factory.Store.Seed("native note", [1f, 0f]);
        factory.Embeddings.Vectors["edited by native client"] = [0.9f, 0.1f];

        var response = await RawHttpProbe.SendAsync(
            factory.NewClient().BaseAddress!,
            "PATCH",
            $"/api/memories/{record.Id}",
            host: "127.0.0.1",
            body: """{"content":"edited by native client","reason":"native client has no Origin"}""");

        response.Status.ShouldBe(HttpStatusCode.OK);
        factory.Store.Records.Single(r => r.Id == record.Id).Content.ShouldBe("edited by native client");
    }

    [Fact]
    public async Task Get_WithNonLoopbackOrigin_Unaffected()
    {
        using var factory = new GrimoireApiFactory();
        factory.Store.Seed("readable note", [1f, 0f]);

        var response = await RawHttpProbe.SendAsync(
            factory.NewClient().BaseAddress!,
            "GET",
            "/api/memories",
            host: "127.0.0.1",
            origin: "http://evil.example");

        response.Status.ShouldBe(HttpStatusCode.OK);
        response.Raw.ShouldContain("readable note");
    }

    [Theory]
    [InlineData("http://127.0.0.1:4770")]
    [InlineData("http://localhost:4770")]
    [InlineData("http://grimoire.localhost:4770")]
    [InlineData("http://grimoire.localhost")]
    public async Task Patch_WithLoopbackOrigin_Accepted(string origin)
    {
        using var factory = new GrimoireApiFactory();
        var record = factory.Store.Seed("browser note", [1f, 0f]);
        factory.Embeddings.Vectors["edited from spa"] = [0.9f, 0.1f];

        var response = await RawHttpProbe.SendAsync(
            factory.NewClient().BaseAddress!,
            "PATCH",
            $"/api/memories/{record.Id}",
            host: "127.0.0.1",
            origin: origin,
            body: """{"content":"edited from spa","reason":"spa edit"}""");

        response.Status.ShouldBe(HttpStatusCode.OK);
        factory.Store.Records.Single(r => r.Id == record.Id).Content.ShouldBe("edited from spa");
    }
}