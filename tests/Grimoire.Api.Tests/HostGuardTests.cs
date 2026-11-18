using System.Net;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class HostGuardTests
{
    [Theory]
    [InlineData("audit-invalid.example")]
    [InlineData("audit-invalid.example:4770")]
    [InlineData("127.0.0.1.evil.example")]
    [InlineData("localhost.evil.example")]
    public async Task Get_WithHostileHostHeader_RefusedWith400(string host)
    {
        using var factory = new GrimoireApiFactory();

        var status = await RawGetAsync(factory.NewClient().BaseAddress!, "/api/version", host);

        status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_WithHostileHostHeader_RefusedBeforeTheEndpointRuns()
    {
        using var factory = new GrimoireApiFactory();
        var storeListed = false;
        factory.Store.OnListAsync = _ => storeListed = true;

        var status = await RawGetAsync(factory.NewClient().BaseAddress!, "/api/memories", "audit-invalid.example");

        status.ShouldBe(HttpStatusCode.BadRequest);
        storeListed.ShouldBeFalse();

        // Positive control: the same route under an allowed Host must reach the store,
        // so the refusal assertion above cannot go vacuously green after a refactor.
        var allowed = await factory.NewClient().GetAsync("/api/memories");
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        storeListed.ShouldBeTrue();
    }

    // The harness binds an ephemeral port (Port = 0); the fixed 4770 in the Host header
    // proves port-less allowlist entries accept any port.
    [Theory]
    [InlineData("127.0.0.1:4770")]
    [InlineData("localhost:4770")]
    [InlineData("grimoire.localhost:4770")]
    public async Task Get_WithLoopbackHostHeader_ServesTheEndpoint(string host)
    {
        using var factory = new GrimoireApiFactory();

        var status = await RawGetAsync(factory.NewClient().BaseAddress!, "/api/version", host);

        status.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<HttpStatusCode> RawGetAsync(Uri baseAddress, string path, string host) =>
        (await RawHttpProbe.SendAsync(baseAddress, "GET", path, host)).Status;
}