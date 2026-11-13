using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class SessionsEndpointsTests(GrimoireApiFactory factory) : IClassFixture<GrimoireApiFactory>, IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    private readonly string _exportRoot = Path.Combine(Path.GetTempPath(), "grimoire-api-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_exportRoot))
        {
            Directory.Delete(_exportRoot, true);
        }
    }

    [Fact]
    public async Task Get_ReturnsRowsNewestFirstWithAgesFromPinnedClock()
    {
        factory.Reset();
        factory.Clock.MoveTo(Now);
        factory.Sessions.Seed(Entry(Now.AddHours(-6), "qwen", "2026-07-28", "guldana", "qwen"));
        factory.Sessions.Seed(Entry(Now.AddHours(-1), "claude", "2025-06-18", "tucker", "claude"));
        factory.Sessions.Seed(Entry(Now.AddHours(-3), null, null, null, null));

        var response = await factory.NewClient().GetAsync("/api/sessions");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        var items = body.GetProperty("items");
        items.GetArrayLength().ShouldBe(3);
        items[0].GetProperty("clientName").GetString().ShouldBe("claude");
        items[0].GetProperty("protocolVersion").GetString().ShouldBe("2025-06-18");
        items[0].GetProperty("actorAlias").GetString().ShouldBe("tucker");
        items[0].GetProperty("actorVendor").GetString().ShouldBe("claude");
        items[0].GetProperty("host").GetString().ShouldBe("workstation");
        items[0].GetProperty("occurredAt").GetDateTimeOffset().ShouldBe(Now.AddHours(-1));
        items[0].GetProperty("ageHours").GetDouble().ShouldBe(1);
        items[1].GetProperty("clientName").ValueKind.ShouldBe(JsonValueKind.Null);
        items[1].GetProperty("ageHours").GetDouble().ShouldBe(3);
        items[2].GetProperty("clientName").GetString().ShouldBe("qwen");
        items[2].GetProperty("ageHours").GetDouble().ShouldBe(6);
    }

    [Fact]
    public async Task Get_NullProtocolVersionSerializesAsJsonNull()
    {
        factory.Reset();
        factory.Clock.MoveTo(Now);
        factory.Sessions.Seed(Entry(Now.AddHours(-2), clientName: "qwen", protocolVersion: null));

        var raw = await factory.NewClient().GetStringAsync("/api/sessions");

        raw.ShouldContain("\"protocolVersion\":null");
        raw.ShouldNotContain("unknown");
        var body = JsonDocument.Parse(raw).RootElement.GetProperty("items");
        body[0].GetProperty("protocolVersion").ValueKind.ShouldBe(JsonValueKind.Null);
        body[0].GetProperty("clientName").GetString().ShouldBe("qwen");
    }

    [Fact]
    public async Task Get_DefaultsLimitToRowsTheRegistryIsAskedFor()
    {
        factory.Reset();
        factory.Clock.MoveTo(Now);
        for (var minute = 1; minute <= 51; minute++)
        {
            factory.Sessions.Seed(Entry(Now.AddMinutes(-minute), $"client-{minute}", "2026-07-28"));
        }

        var response = await factory.NewClient().GetAsync("/api/sessions");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("limit").GetInt32().ShouldBe(50);
        body.GetProperty("items").GetArrayLength().ShouldBe(50);
        factory.Sessions.LastLimit.ShouldBe(50);
    }

    [Fact]
    public async Task Get_DefaultsWindowToTheConfiguredRetention()
    {
        using var scoped = GrimoireApiFactory.Create(sessionsRetentionHours: 6);
        scoped.Reset();
        scoped.Clock.MoveTo(Now);
        scoped.Sessions.Seed(Entry(Now.AddHours(-2), "inside", "2026-07-28"));
        scoped.Sessions.Seed(Entry(Now.AddHours(-8), "outside", "2026-07-28"));

        var response = await scoped.NewClient().GetAsync("/api/sessions");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("windowHours").GetDouble().ShouldBe(6);
        body.GetProperty("items").GetArrayLength().ShouldBe(1);
        body.GetProperty("items")[0].GetProperty("clientName").GetString().ShouldBe("inside");
        scoped.Sessions.LastNotBefore.ShouldBe(Now.AddHours(-6));
    }

    [Fact]
    public async Task Get_ExplicitQueryValuesOverrideTheDefaults()
    {
        factory.Reset();
        factory.Clock.MoveTo(Now);
        factory.Sessions.Seed(Entry(Now.AddHours(-30), "aged", "2026-07-28", "guldana", "qwen"));
        factory.Sessions.Seed(Entry(Now.AddHours(-1), "recent", "2026-07-28", "tucker", "claude"));

        var response = await factory.NewClient().GetAsync("/api/sessions?limit=1&windowHours=48");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("limit").GetInt32().ShouldBe(1);
        body.GetProperty("windowHours").GetDouble().ShouldBe(48);
        body.GetProperty("items").GetArrayLength().ShouldBe(1);
        body.GetProperty("items")[0].GetProperty("clientName").GetString().ShouldBe("recent");
        factory.Sessions.LastNotBefore.ShouldBe(Now.AddHours(-48));
    }

    [Fact]
    public async Task Get_EmptyRegistryReturnsEmptyItems()
    {
        factory.Reset();
        factory.Clock.MoveTo(Now);

        var response = await factory.NewClient().GetAsync("/api/sessions");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("items").GetArrayLength().ShouldBe(0);
        body.GetProperty("degradationReason").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Get_DegradedRegistryStillAnswersWithTheReason()
    {
        factory.Reset();
        factory.Clock.MoveTo(Now);
        factory.Sessions.DegradationReason = "Session registry is unreachable — recent handshakes may be missing: down";

        var response = await factory.NewClient().GetAsync("/api/sessions");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("items").GetArrayLength().ShouldBe(0);
        body.GetProperty("degradationReason").GetString().ShouldStartWith("Session registry is unreachable");
    }

    [Fact]
    public async Task Get_WhileSpaExportIsInstalled_LiteralRouteWinsOverTheFallback()
    {
        // The sessions route outranks the SPA catch-all: /api answers JSON while a shell path on
        // the same host still answers index.html. Registration order is NOT what makes this hold —
        // SpaHosting installs app.MapFallback and a literal route beats a fallback either way
        // (moving the mapping behind UseSpaFrontend keeps the suite green) — so this pins the
        // route split and dies if the fallback ever swallows /api paths.
        Directory.CreateDirectory(_exportRoot);
        await File.WriteAllTextAsync(
            Path.Combine(_exportRoot, "index.html"),
            "<html><title>grimoire spa shell</title></html>");
        using var scoped = GrimoireApiFactory.Create(staticRootPath: _exportRoot);
        scoped.Reset();
        scoped.Clock.MoveTo(Now);
        scoped.Sessions.Seed(Entry(Now.AddHours(-4), "qwen", "2026-07-28"));
        var client = scoped.NewClient();

        var sessions = await client.GetAsync("/api/sessions");
        var shell = await client.GetAsync("/memories");
        var root = await client.GetAsync("/");

        sessions.StatusCode.ShouldBe(HttpStatusCode.OK);
        sessions.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        var body = await sessions.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("items")[0].GetProperty("clientName").GetString().ShouldBe("qwen");
        shell.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await shell.Content.ReadAsStringAsync()).ShouldContain("grimoire spa shell");
        root.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await root.Content.ReadAsStringAsync()).ShouldContain("grimoire spa shell");
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e999")]
    [InlineData("-1")]
    [InlineData("99999")]
    public async Task Get_NonUsableWindowHours_Returns400AndReadsNothing(string value)
    {
        factory.Reset();
        factory.Clock.MoveTo(Now);
        factory.Sessions.Seed(Entry(Now.AddHours(-1), "qwen", "2026-07-28"));

        var response = await factory.NewClient().GetAsync($"/api/sessions?windowHours={value}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var (code, message) = await ReadError(response);
        code.ShouldBe("invalid_request");
        message.ShouldContain("windowHours");
        factory.Sessions.LastLimit.ShouldBeNull();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("501")]
    public async Task Get_OutOfRangeLimit_Returns400AndReadsNothing(string value)
    {
        factory.Reset();
        factory.Clock.MoveTo(Now);
        factory.Sessions.Seed(Entry(Now.AddHours(-1), "qwen", "2026-07-28"));

        var response = await factory.NewClient().GetAsync($"/api/sessions?limit={value}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var (code, message) = await ReadError(response);
        code.ShouldBe("invalid_request");
        message.ShouldContain("limit");
        factory.Sessions.LastLimit.ShouldBeNull();
    }

    [Theory]
    [InlineData("windowHours=abc")]
    [InlineData("limit=abc")]
    [InlineData("limit=NaN")]
    [InlineData("limit=1.5")]
    [InlineData("limit=99999999999")]
    public async Task Get_UnparseableQueryValue_ReturnsStructured400(string query)
    {
        factory.Reset();
        factory.Clock.MoveTo(Now);

        var response = await factory.NewClient().GetAsync($"/api/sessions?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        (await ReadError(response)).code.ShouldBe("invalid_request");
        factory.Sessions.LastLimit.ShouldBeNull();
    }

    [Theory]
    [InlineData("limit=1", 1, 24)]
    [InlineData("limit=500", 500, 24)]
    [InlineData("windowHours=0", 50, 0)]
    [InlineData("windowHours=8760", 50, 8760)]
    public async Task Get_AtTheDeclaredBounds_AnswersWithTheRequestedWindow(string query, int limit, double windowHours)
    {
        factory.Reset();
        factory.Clock.MoveTo(Now);
        factory.Sessions.Seed(Entry(Now.AddHours(-1), "qwen", "2026-07-28"));

        var response = await factory.NewClient().GetAsync($"/api/sessions?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
        body.GetProperty("limit").GetInt32().ShouldBe(limit);
        body.GetProperty("windowHours").GetDouble().ShouldBe(windowHours);
    }

    [Fact]
    public async Task Post_MethodMismatchReturns405WithAllow()
    {
        factory.Reset();
        factory.Clock.MoveTo(Now);

        var response = await factory.NewClient().PostAsync("/api/sessions", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        response.Content.Headers.Allow.ShouldBe(["GET"]);
    }

    private static async Task<(string code, string message)> ReadError(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(stream, TestJson.Options);
        var error = body.GetProperty("error");
        return (error.GetProperty("code").GetString()!, error.GetProperty("message").GetString()!);
    }

    private static SessionRegistryEntry Entry(
        DateTimeOffset occurredAt,
        string? clientName,
        string? protocolVersion,
        string? actorAlias = "guldana",
        string? actorVendor = "qwen") => new()
        {
            OccurredAt = occurredAt,
            ClientName = clientName,
            ClientVersion = clientName is null ? null : "1.0.0",
            ProtocolVersion = protocolVersion,
            ActorAlias = actorAlias,
            ActorVendor = actorVendor,
            Transport = "stdio",
            Host = "workstation",
            SessionId = null,
        };
}
