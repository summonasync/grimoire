using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grimoire.Api.Contracts;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class SettingsEndpointsTests(GrimoireApiFactory factory) : IClassFixture<GrimoireApiFactory>
{
    [Fact]
    public async Task Get_ExposesEngineAndHostTuning()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync("/api/settings");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var settings = await response.Content.ReadFromJsonAsync<SettingsResponse>(TestJson.Options);
        settings.ShouldNotBeNull();
        settings.Memory.SessionTtlHours.ShouldBe(24);
        settings.Memory.DedupSimilarityThreshold.ShouldBe(0.92);
        settings.Memory.TombstoneRetentionDays.ShouldBe(30);
        settings.Memory.RecallOversampling.ShouldBe(3);
        settings.Memory.RecencyHalfLives.SessionDays.ShouldBe(0.5);
        settings.Memory.RecencyHalfLives.PersonalDays.ShouldBe(180);
        settings.Memory.Compaction.MinSimilarity.ShouldBe(0.85);
        settings.Memory.Compaction.MinClusterSize.ShouldBe(2);
        settings.Serve.Port.ShouldBe(0);
        settings.Serve.GcIntervalMinutes.ShouldBe(60);
        settings.Persistence.Available.ShouldBeFalse();
        settings.Persistence.Reason.ShouldBe("settings_persistence_not_implemented");
        settings.Editable.ShouldContain("memory.sessionTtlHours");
    }

    [Fact]
    public async Task Get_ReflectsConfiguredEngineOptions()
    {
        factory.Reset();
        using var configured = GrimoireApiFactory.Create(new MemoryEngineOptions
        {
            SessionTtlHours = 5,
            RecallOversampling = 2,
        });

        var settings = await configured.NewClient().GetFromJsonAsync<SettingsResponse>("/api/settings", TestJson.Options);

        settings!.Memory.SessionTtlHours.ShouldBe(5);
        settings.Memory.RecallOversampling.ShouldBe(2);
    }

    [Fact]
    public async Task Put_Returns501WithStructuredDeferral()
    {
        factory.Reset();

        var response = await factory.NewClient().PutAsJsonAsync("/api/settings", new { sessionTtlHours = 12 });

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
        var error = await ReadError(response);
        error.code.ShouldBe("settings_persistence_not_implemented");
        error.message.ShouldContain("config file");
    }

    [Fact]
    public async Task Put_WithMalformedBody_Returns400()
    {
        factory.Reset();
        using var content = new StringContent("{not json", System.Text.Encoding.UTF8, "application/json");

        var response = await factory.NewClient().PutAsync("/api/settings", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await ReadError(response);
        error.code.ShouldBe("invalid_request");
        error.message.ShouldNotBeEmpty();
    }

    private static async Task<(string code, string message)> ReadError(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var error = body.GetProperty("error");
        return (error.GetProperty("code").GetString()!, error.GetProperty("message").GetString()!);
    }
}
