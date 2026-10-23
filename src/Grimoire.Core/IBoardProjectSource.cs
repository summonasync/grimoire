namespace Grimoire.Core;

public interface IBoardProjectSource
{
    Task<IReadOnlyList<BoardProject>> FetchAsync(CancellationToken cancellationToken);
}
