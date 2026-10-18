using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class PersonaEndpointsTests(GrimoireApiFactory factory) : IClassFixture<GrimoireApiFactory>
{
    [Fact]
    public async Task Get_WithoutPersona_ReturnsEmptyDocument()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync("/api/persona");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = await ReadDocument(response);
        document.Identity.ShouldBeEmpty();
        document.Tone.ShouldBeEmpty();
        document.Preferences.ShouldBeEmpty();
        document.Rules.ShouldBeEmpty();
    }

    [Fact]
    public async Task Put_ThenGet_RoundTripsDocument()
    {
        factory.Reset();
        RegisterSectionEmbeddings("Ada", "dry humor", "vim keybindings", "confirm before deletes");

        var client = factory.NewClient();
        var putResponse = await client.PutAsJsonAsync("/api/persona", new
        {
            identity = "Ada",
            tone = "dry humor",
            preferences = "vim keybindings",
            rules = "confirm before deletes",
        });

        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stored = await ReadDocument(await client.GetAsync("/api/persona"));

        stored.Identity.ShouldBe("Ada");
        stored.Tone.ShouldBe("dry humor");
        stored.Preferences.ShouldBe("vim keybindings");
        stored.Rules.ShouldBe("confirm before deletes");
    }

    [Fact]
    public async Task Put_WritesHumanProvenance()
    {
        factory.Reset();
        RegisterSectionEmbeddings("Ada", string.Empty, string.Empty, string.Empty);

        await factory.NewClient().PutAsJsonAsync("/api/persona", new { identity = "Ada" });

        factory.Store.Records.ShouldNotBeEmpty();
        factory.Store.Records.All(record => record.Actor.IsHuman).ShouldBeTrue();
        factory.Store.Records.All(record => record.Tags.Contains("persona")).ShouldBeTrue();
        factory.Store.Records.All(record => record.Tome == TomeKey.DefaultTome).ShouldBeTrue();
    }

    [Fact]
    public async Task Put_EchoesTheResolvedTome()
    {
        using var tomeFactory = GrimoireApiFactory.Create(envTome: "eng");
        tomeFactory.Reset();
        tomeFactory.Embeddings.Vectors["Ada"] = [1f, 0f];

        var response = await tomeFactory.NewClient().PutAsJsonAsync("/api/persona", new { identity = "Ada" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("tome").GetString().ShouldBe("eng");
        body.GetProperty("document").GetProperty("identity").GetString().ShouldBe("Ada");
        tomeFactory.Store.Records.Single().Tome.ShouldBe("eng");
    }

    [Fact]
    public async Task Put_WithTomeParameter_WritesIntoThatTome()
    {
        factory.Reset();
        RegisterSectionEmbeddings("Ada");

        var response = await factory.NewClient().PutAsJsonAsync("/api/persona?tome=eng", new { identity = "Ada" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        factory.Store.Records.Single().Tome.ShouldBe("eng");
    }

    [Fact]
    public async Task Get_WithTomeParameter_IsolatesPersonaDocuments()
    {
        factory.Reset();
        RegisterSectionEmbeddings("Ada");

        await factory.NewClient().PutAsJsonAsync("/api/persona?tome=eng", new { identity = "Ada" });

        var home = await ReadDocument(await factory.NewClient().GetAsync("/api/persona"));
        var eng = await ReadDocument(await factory.NewClient().GetAsync("/api/persona?tome=eng"));

        home.Identity.ShouldBeEmpty();
        eng.Identity.ShouldBe("Ada");
    }

    [Fact]
    public async Task PersonaEndpoints_WithGarbageTome_Returns400()
    {
        factory.Reset();

        var getResponse = await factory.NewClient().GetAsync("/api/persona?tome=Not%20Valid%21%21");
        var putResponse = await factory.NewClient().PutAsJsonAsync("/api/persona?tome=Not%20Valid%21%21", new { identity = "Ada" });

        getResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        putResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(getResponse)).ShouldBe("invalid_tome");
        (await ReadError(putResponse)).ShouldBe("invalid_tome");
    }

    private void RegisterSectionEmbeddings(params string[] contents)
    {
        foreach (var content in contents)
        {
            factory.Embeddings.Vectors[content] = [1f, 0f];
        }
    }

    private static async Task<PersonaDocument> ReadDocument(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        var document = await JsonSerializer.DeserializeAsync<PersonaDocument>(stream, TestJson.Options);
        return document!;
    }

    private static async Task<string> ReadError(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(stream, TestJson.Options);
        return body.GetProperty("error").GetProperty("code").GetString()!;
    }
}
