using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Grimoire.Api.Tests;

// HttpClient rewrites an explicit Host header to the request URI's authority, so header
// regressions probe over a raw socket — the same wire shape as a curl probe.
internal static class RawHttpProbe
{
    public static async Task<RawHttpResponse> SendAsync(
        Uri baseAddress,
        string method,
        string path,
        string host,
        string[]? origins = null,
        string? body = null)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(baseAddress.Host, baseAddress.Port, cts.Token);
        await using var stream = tcp.GetStream();

        var request = new StringBuilder($"{method} {path} HTTP/1.1\r\nHost: {host}\r\nConnection: close\r\n");
        if (origins is not null)
        {
            foreach (var origin in origins)
            {
                request.Append($"Origin: {origin}\r\n");
            }
        }
        if (body is not null)
        {
            request.Append($"Content-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\n");
        }
        request.Append("\r\n");
        if (body is not null)
        {
            request.Append(body);
        }

        await stream.WriteAsync(Encoding.UTF8.GetBytes(request.ToString()), cts.Token);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var statusLine = await reader.ReadLineAsync(cts.Token);
        var rest = await reader.ReadToEndAsync(cts.Token);
        return new RawHttpResponse((HttpStatusCode)int.Parse(statusLine!.Split(' ')[1]), rest);
    }
}

internal sealed record RawHttpResponse(HttpStatusCode Status, string Raw);