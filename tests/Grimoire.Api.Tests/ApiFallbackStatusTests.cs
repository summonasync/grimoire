using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class ApiFallbackStatusTests : IDisposable
{
    private readonly string _exportRoot = Path.Combine(Path.GetTempPath(), "grimoire-api-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_exportRoot))
        {
            Directory.Delete(_exportRoot, true);
        }
    }

    [Fact]
    public async Task MethodMismatch_Returns405WithAllow()
    {
        using var factory = new GrimoireApiFactory();

        var response = await factory.NewClient().PostAsync("/api/version", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        response.Content.Headers.Allow.ShouldContain("GET");
        (await response.Content.ReadAsStringAsync()).ShouldContain("method_not_allowed");
    }

    [Fact]
    public async Task MethodMismatch_AllowJoinsMethodsOfAllEndpointsOnPath()
    {
        using var factory = new GrimoireApiFactory();
        var client = factory.NewClient();

        var settings = await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, "/api/settings"));
        var memory = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/memories"));

        settings.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        settings.Content.Headers.Allow.ShouldBe(["GET", "PUT"], ignoreOrder: true);
        memory.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        memory.Content.Headers.Allow.ShouldBe(["GET", "POST"], ignoreOrder: true);
    }

    [Fact]
    public async Task UnsupportedMediaType_Returns415()
    {
        using var factory = new GrimoireApiFactory();

        var response = await factory.NewClient().PostAsync(
            "/api/memories",
            new StringContent("{}", Encoding.UTF8, new MediaTypeHeaderValue("text/plain")));

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("")]
    public async Task MalformedOrMissingJsonBody_Returns400ThatBlamesTheBody(string payload)
    {
        using var factory = new GrimoireApiFactory();

        var response = await factory.NewClient()
            .PostAsync("/api/memories", new StringContent(payload, Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync())
            .ShouldContain("The request body is missing, malformed, or does not match the expected schema.");
    }

    [Fact]
    public async Task UnparseableQueryOnBodylessRoute_Returns400ThatNamesNoBody()
    {
        using var factory = new GrimoireApiFactory();

        var response = await factory.NewClient().GetAsync("/api/agents?windowDays=abc");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldContain("query parameter");
        raw.ShouldNotContain("body");
    }

    [Fact]
    public async Task UnknownApiPath_Returns404NotFoundPayload()
    {
        using var factory = new GrimoireApiFactory();

        var response = await factory.NewClient().GetAsync("/api/nope");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        (await response.Content.ReadAsStringAsync()).ShouldContain("not_found");
    }

    [Fact]
    public async Task GuidConstraintRejectsSegment_Returns404()
    {
        using var factory = new GrimoireApiFactory();

        var response = await factory.NewClient().GetAsync("/api/memories/not-a-guid");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain("not_found");
    }

    [Fact]
    public async Task SpaRouteUnderInstalledExport_ServesShell()
    {
        Directory.CreateDirectory(_exportRoot);
        await File.WriteAllTextAsync(Path.Combine(_exportRoot, "index.html"), "<html><title>grimoire spa</title></html>");
        using var factory = GrimoireApiFactory.Create(staticRootPath: _exportRoot);

        var response = await factory.NewClient().GetAsync("/memories");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("grimoire spa");
    }
}
