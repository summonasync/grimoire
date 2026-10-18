using Grimoire.Core;

namespace Grimoire.Infrastructure.Configuration;

public sealed class ActorClaimProvider(string? startDirectory = null) : IActorClaimProvider
{
    public const string FileName = "AGENTS.md";

    public Task<ActorClaim?> ReadAsync(CancellationToken cancellationToken)
    {
        foreach (var directory in Chain(Path.GetFullPath(startDirectory ?? Environment.CurrentDirectory)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var claim = Read(directory);
            if (claim is not null)
            {
                return Task.FromResult<ActorClaim?>(claim);
            }
        }

        return Task.FromResult<ActorClaim?>(null);
    }

    private static ActorClaim? Read(string directory)
    {
        var path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return ActorClaimBlockParser.Parse(File.ReadAllText(path));
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<string> Chain(string start)
    {
        var current = start;
        while (true)
        {
            yield return current;

            var parent = Path.GetDirectoryName(current);
            if (parent is null || parent == current)
            {
                yield break;
            }

            current = parent;
        }
    }
}
