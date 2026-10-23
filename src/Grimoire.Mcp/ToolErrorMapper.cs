using Grimoire.Core;

namespace Grimoire.Mcp;

internal static class ToolErrorMapper
{
    public static ToolError Map(Exception exception) => exception switch
    {
        ProjectRegistryMutationException registryMutation => new ToolError(
            ToolErrorCodes.InvalidRequest,
            FirstLine(registryMutation.Message)).NothingApplied(),
        UpstreamUnavailableException upstream => new ToolError(
            ToolErrorCodes.UpstreamUnavailable,
            $"Upstream '{upstream.Upstream}' is unavailable: not reachable, it timed out, or it rejected the configured credentials. " +
            "Nothing was written by this call. Check the service and its configuration, then retry."),
        ReindexRequiredException reindexRequired => new ToolError(
            ToolErrorCodes.ReindexRequired,
            FirstLine(reindexRequired.Message)),
        ArgumentException argumentException => new ToolError(
            ToolErrorCodes.InvalidRequest,
            FirstLine(argumentException.Message),
            argumentException.ParamName),
        System.Net.Http.HttpRequestException or System.Net.Sockets.SocketException or TaskCanceledException => new ToolError(
            ToolErrorCodes.UpstreamUnavailable,
            "An upstream service (embedding provider or vector store) is unreachable. Nothing was written by this call; retry when the service is back. " + FirstLine(exception.Message)),
        _ => new ToolError(
            ToolErrorCodes.InternalError,
            "Unexpected internal failure. " + FirstLine(exception.Message)),
    };

    private static string FirstLine(string message)
    {
        var line = message.Split('\n')[0].TrimEnd('\r');
        return string.IsNullOrWhiteSpace(line) ? "No diagnostics were provided." : line;
    }
}
