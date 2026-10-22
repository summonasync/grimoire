using System.Net;
using System.Net.Sockets;
using Grimoire.Core;
using Grimoire.Infrastructure.Processes;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Processes;

public sealed class LoopbackConsoleProbeTests
{
    [Fact]
    public async Task IsAliveAsync_HealthAnsweringOk_ReturnsTrueAndTargetsLoopbackLiteral()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var probe = new LoopbackConsoleProbe(new HttpClient(handler));

        var alive = await probe.IsAliveAsync(5099, CancellationToken.None);

        alive.ShouldBeTrue();
        handler.LastRequestUri.ShouldBe("http://127.0.0.1:5099/health");
    }

    [Fact]
    public async Task IsAliveAsync_HealthDegraded_ReturnsFalse()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var probe = new LoopbackConsoleProbe(new HttpClient(handler));

        (await probe.IsAliveAsync(5099, CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task IsAliveAsync_TransportFailure_ReturnsFalse()
    {
        var handler = new RecordingHandler(_ => throw new HttpRequestException("connection refused"));
        var probe = new LoopbackConsoleProbe(new HttpClient(handler));

        (await probe.IsAliveAsync(5099, CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task IsAliveAsync_PortWithNoListener_ReturnsFalseWithoutThrowing()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var probe = new LoopbackConsoleProbe(new HttpClient());

        var alive = await probe.IsAliveAsync(port, CancellationToken.None);

        alive.ShouldBeFalse();
    }

    [Fact]
    public async Task IsAliveAsync_CallerCancellation_Propagates()
    {
        using var timeout = new CancellationTokenSource();
        await timeout.CancelAsync();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var probe = new LoopbackConsoleProbe(new HttpClient(handler));

        await Should.ThrowAsync<OperationCanceledException>(() => probe.IsAliveAsync(5099, timeout.Token));
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public string? LastRequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            return respond(request);
        }
    }
}
