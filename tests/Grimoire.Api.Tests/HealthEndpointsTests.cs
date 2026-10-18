using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class HealthEndpointsTests(GrimoireApiFactory factory) : IClassFixture<GrimoireApiFactory>
{
    [Fact]
    public async Task Get_WhenStoreAndEmbeddingsAnswer_ReportsHealthy()
    {
        factory.Reset();
        factory.Embeddings.Vectors["grimoire-health-probe"] = [1f, 0f];

        var response = await factory.NewClient().GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("status").GetString().ShouldBe("healthy");
        body.GetProperty("store").GetProperty("reachable").GetBoolean().ShouldBeTrue();
        body.GetProperty("embeddings").GetProperty("reachable").GetBoolean().ShouldBeTrue();
        body.GetProperty("checkedAt").GetDateTimeOffset().ShouldBe(factory.Clock.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Get_WhenEmbeddingsFail_ReportsDegraded()
    {
        factory.Reset();
        factory.Embeddings.Vectors.Remove("grimoire-health-probe");

        var response = await factory.NewClient().GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("status").GetString().ShouldBe("degraded");
        body.GetProperty("store").GetProperty("reachable").GetBoolean().ShouldBeTrue();
        body.GetProperty("embeddings").GetProperty("reachable").GetBoolean().ShouldBeFalse();
        body.GetProperty("embeddings").GetProperty("error").GetString().ShouldNotBeNull();
    }
}
