using System.Threading.Channels;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

internal sealed class ChannelClientTransport(ChannelReader<JsonRpcMessage> reader, ChannelWriter<JsonRpcMessage> writer) : IClientTransport
{
    public string Name => "in-proc";

    public Task<ITransport> ConnectAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ITransport>(new ChannelTransport(reader, writer));
    }
}
