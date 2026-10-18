using System.Net;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class StaticFrontendTests : IDisposable
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
    public async Task MissingExport_ServesFallbackPage()
    {
        using var factory = new GrimoireApiFactory();

        var response = await factory.NewClient().GetAsync("/");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        (await response.Content.ReadAsStringAsync()).ShouldContain("web UI is not deployed");
    }

    [Fact]
    public async Task MissingExport_UnknownApiPath_Returns404Json()
    {
        using var factory = new GrimoireApiFactory();

        var response = await factory.NewClient().GetAsync("/api/does-not-exist");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        (await response.Content.ReadAsStringAsync()).ShouldContain("not_found");
    }

    [Fact]
    public async Task ExistingExport_ServesFilesAndSpaFallback()
    {
        Directory.CreateDirectory(_exportRoot);
        await File.WriteAllTextAsync(Path.Combine(_exportRoot, "index.html"), "<html><title>grimoire spa</title></html>");
        await File.WriteAllTextAsync(Path.Combine(_exportRoot, "app.css"), "body { color: #111; }");
        using var factory = GrimoireApiFactory.Create(staticRootPath: _exportRoot);
        var client = factory.NewClient();

        var root = await client.GetAsync("/");
        var asset = await client.GetAsync("/app.css");
        var spaRoute = await client.GetAsync("/memories");
        var missingAsset = await client.GetAsync("/missing.js");

        root.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await root.Content.ReadAsStringAsync()).ShouldContain("grimoire spa");
        asset.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await asset.Content.ReadAsStringAsync()).ShouldContain("#111");
        spaRoute.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await spaRoute.Content.ReadAsStringAsync()).ShouldContain("grimoire spa");
        missingAsset.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
