using Grimoire.Core;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tests;

public sealed class ToolResultsTests
{
    [Fact]
    public async Task ExecuteAsync_ExceptionWhileCallerCancelled_PropagatesInsteadOfReporting()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<UpstreamUnavailableException>(() => ToolResults.ExecuteAsync(
            _ => throw new UpstreamUnavailableException("qdrant"),
            cancellation.Token));
    }

    [Fact]
    public async Task ExecuteAsync_ExceptionWithLiveCaller_MapsToErrorEnvelope()
    {
        var result = await ToolResults.ExecuteAsync(
            _ => throw new UpstreamUnavailableException("qdrant"),
            CancellationToken.None);

        result.IsError.ShouldBe(true);
        var (code, message, _) = result.Error();
        code.ShouldBe(ToolErrorCodes.UpstreamUnavailable);
        message.ShouldContain("qdrant");
        message.ShouldNotContain("Exception");
    }
}
