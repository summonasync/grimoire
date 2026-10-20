using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class VersionEndpointsTests : IDisposable
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
    public async Task Get_ReportsTheAssemblyVersionInThreeParts()
    {
        using var factory = new GrimoireApiFactory();

        var response = await factory.NewClient().GetAsync("/api/version");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var reported = body.GetProperty("version").GetString().ShouldNotBeNull();
        reported.ShouldBe(typeof(ApiHostBuilder).Assembly.GetName().Version!.ToString(3));
        Version.TryParse(reported, out var parsed).ShouldBeTrue();
        parsed.ToString(3).ShouldBe(reported);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(["version"]);
    }

    [Fact]
    public async Task Get_WithTheSpaDeployed_TheApiRouteServesJsonAndTheSpaStillServesItsShell()
    {
        Directory.CreateDirectory(_exportRoot);
        await File.WriteAllTextAsync(Path.Combine(_exportRoot, "index.html"), "<html><title>grimoire spa</title></html>");
        using var factory = GrimoireApiFactory.Create(staticRootPath: _exportRoot);
        var client = factory.NewClient();

        var version = await client.GetAsync("/api/version");
        var spaRoute = await client.GetAsync("/memories");

        version.StatusCode.ShouldBe(HttpStatusCode.OK);
        version.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        spaRoute.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await spaRoute.Content.ReadAsStringAsync()).ShouldContain("grimoire spa");
    }
}
