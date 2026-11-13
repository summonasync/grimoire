using System.Net;
using Grimoire.Core;
using ModelContextProtocol.Server;

namespace Grimoire.Mcp;

internal static class SessionHandshakeRow
{
    public const string StdioTransport = "stdio";

    public static SessionRegistryEntry Build(
        McpServer server,
        MemoryActor actor,
        DateTimeOffset occurredAt,
        string transport) => new()
        {
            OccurredAt = occurredAt,
            ClientName = server.ClientInfo?.Name,
            ClientVersion = server.ClientInfo?.Version,
            ProtocolVersion = server.NegotiatedProtocolVersion,
            ActorAlias = actor.Alias,
            ActorVendor = actor.Vendor,
            Transport = transport,
            Host = Dns.GetHostName(),
            SessionId = server.SessionId,
        };
}
