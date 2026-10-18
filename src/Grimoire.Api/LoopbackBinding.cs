using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Grimoire.Api;

public static class LoopbackBinding
{
    public static IPEndPoint Endpoint(int port) => new(IPAddress.Loopback, port);

    public static void Configure(KestrelServerOptions kestrel, int port) => kestrel.Listen(Endpoint(port));
}
