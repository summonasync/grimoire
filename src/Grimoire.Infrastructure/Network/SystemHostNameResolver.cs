using System.Net;
using System.Net.Sockets;
using Grimoire.Core;

namespace Grimoire.Infrastructure.Network;

public sealed class SystemHostNameResolver : IHostNameResolver
{
    // The solution's only Dns consumer: callers decide display names through this port so the
    // branch stays fakeable in tests. A name that does not resolve is an empty answer, not a
    // fault — picking the literal loopback fallback is the caller's job, not a thrown error.
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            return await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (SocketException) when (!cancellationToken.IsCancellationRequested)
        {
            return [];
        }
    }
}
