namespace Grimoire.Core.Tests.Fakes;

public sealed class FakeBoardProjectSource(IReadOnlyList<BoardProject>? projects = null) : IBoardProjectSource
{
    public Exception? Failure { get; set; }

    public IReadOnlyList<BoardProject> Projects { get; set; } = projects ?? [];

    public Task<IReadOnlyList<BoardProject>> FetchAsync(CancellationToken cancellationToken)
    {
        if (Failure is { } failure)
        {
            throw failure;
        }

        return Task.FromResult(Projects);
    }
}
