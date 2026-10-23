using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

internal sealed class FakeBoardProjectSource(IReadOnlyList<BoardProject>? projects = null) : IBoardProjectSource
{
    public Exception? Failure { get; set; }

    public int Fetches { get; private set; }

    public IReadOnlyList<BoardProject> Projects { get; set; } = projects ?? [];

    public Task<IReadOnlyList<BoardProject>> FetchAsync(CancellationToken cancellationToken)
    {
        Fetches++;
        if (Failure is { } failure)
        {
            throw failure;
        }

        return Task.FromResult(Projects);
    }
}
