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
}
