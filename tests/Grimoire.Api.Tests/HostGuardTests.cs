using System.Net;
using System.Net.Sockets;
using System.Text;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class HostGuardTests
{
    [Theory]
    [InlineData("audit-invalid.example")]
    [InlineData("audit-invalid.example:4770")]
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

    // HttpClient rewrites an explicit Host header to the request URI's authority, so the
    // probe goes over a raw socket — the same wire shape as the audit's curl probe.
    private static async Task<HttpStatusCode> RawGetAsync(Uri baseAddress, string path, string host)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(baseAddress.Host, baseAddress.Port);
        await using var stream = tcp.GetStream();
        var request = Encoding.ASCII.GetBytes(
            $"GET {path} HTTP/1.1\r\nHost: {host}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(request);
        using var reader = new StreamReader(stream, Encoding.ASCII);
        var statusLine = await reader.ReadLineAsync();
        return (HttpStatusCode)int.Parse(statusLine!.Split(' ')[1]);
    }
}