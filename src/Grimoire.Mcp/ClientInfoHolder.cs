using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Grimoire.Mcp;

internal sealed class ClientInfoHolder
{
    private Func<Implementation?>? _reader;

    public void Bind(McpServer server) => _reader = () => server.ClientInfo;

    public Implementation? Read() => _reader?.Invoke();
}
