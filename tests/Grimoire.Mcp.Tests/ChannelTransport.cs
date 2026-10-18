using System.Threading.Channels;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

internal sealed class ChannelTransport(ChannelReader<JsonRpcMessage> reader, ChannelWriter<JsonRpcMessage> writer) : ITransport
{
    public ChannelReader<JsonRpcMessage> MessageReader => reader;

    public string SessionId => "in-proc";

    public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        return writer.WriteAsync(message, cancellationToken).AsTask();
    }

    public ValueTask DisposeAsync()
    {
        writer.TryComplete();
        return default;
    }
}
