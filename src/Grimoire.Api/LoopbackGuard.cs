using System.Net;

namespace Grimoire.Api;

public static class LoopbackGuard
{
    public static void EnsureLoopbackOnly(IEnumerable<string> addresses)
    {
        foreach (var address in addresses)
        {
            if (!IsLoopback(address))
            {
                throw new InvalidOperationException(
                    $"Refusing to serve: '{address}' is not a loopback bind. The HTTP face may only listen on 127.0.0.0/8 or [::1].");
            }
        }
    }

    private static bool IsLoopback(string address)
    {
        return Uri.TryCreate(address, UriKind.Absolute, out var uri)
            && uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
            && IPAddress.TryParse(uri.IdnHost, out var ip)
            && IPAddress.IsLoopback(ip);
    }
}
