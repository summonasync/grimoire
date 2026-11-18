using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class StructuredErrorsTests
{
    [Fact]
    public async Task Patch_WithAnUnexpectedFailure_ReturnsStructured500WithoutInternals()
    {
        using var factory = new GrimoireApiFactory();
        var record = factory.Store.Seed("doomed note", [1f, 0f]);
        factory.Embeddings.Failure = new InvalidOperationException("secret internal detail");

        var response = await factory.NewClient().PatchAsJsonAsync(
            $"/api/memories/{record.Id}",
            new { content = "rewritten", reason = "forced failure" });

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var raw = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<JsonElement>(raw, TestJson.Options);
        var error = body.GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe("internal_error");
        var message = error.GetProperty("message").GetString().ShouldNotBeNull();
        message.ShouldContain("Correlation id:");
        raw.ShouldNotContain("secret internal detail");
        raw.ShouldNotContain("InvalidOperationException");
        raw.ShouldNotContain("StackTrace");
        raw.ShouldNotContain("at Grimoire");
    }
}