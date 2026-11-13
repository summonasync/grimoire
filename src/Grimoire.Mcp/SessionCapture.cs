using Grimoire.Core;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Grimoire.Mcp;

internal sealed class SessionCapture(
    ISessionRegistry registry,
    MemoryActorProvider actors,
    IClock clock,
    ISessionCaptureFaultSink faults,
    string transport,
    TimeSpan writeBudget)
{
    public static readonly TimeSpan DefaultWriteBudget = TimeSpan.FromSeconds(10);

    private McpServer? root;
    private int captured;

    public void Bind(McpServer server) => root = server;

    public McpMessageHandler Wrap(McpMessageHandler next) =>
        async (context, cancellationToken) =>
    {
        await next(context, cancellationToken);
        if (context.JsonRpcMessage is not JsonRpcRequest
            || root is null
            || Interlocked.Exchange(ref captured, 1) != 0)
        {
            return;
        }

        var occurredAt = clock.UtcNow;
        _ = Task.Run(() => FlushAsync(root, occurredAt));
    };

    private async Task FlushAsync(McpServer server, DateTimeOffset occurredAt)
    {
        using var budget = new CancellationTokenSource(writeBudget);
        try
        {
            var actor = await actors.GetActorAsync(budget.Token);
            await registry.WriteAsync(
                SessionHandshakeRow.Build(server, actor, occurredAt, transport),
                budget.Token);
        }
        catch (Exception failure)
        {
            faults.CaptureFailed(Describe(failure));
        }
    }

    private string Describe(Exception failure) =>
        failure is OperationCanceledException
            ? $"session registry write abandoned after the {writeBudget} write budget"
            : failure.Message;
}
