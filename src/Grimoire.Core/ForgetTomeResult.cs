namespace Grimoire.Core;

public sealed record ForgetTomeResult
{
    public required int Forgotten { get; init; }

    public int Skipped { get; init; }
}
