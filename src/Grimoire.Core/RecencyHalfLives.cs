namespace Grimoire.Core;

public sealed record RecencyHalfLives
{
    public double SessionDays { get; init; } = 0.5;

    public double ProjectDays { get; init; } = 30;

    public double GlobalDays { get; init; } = 90;

    public double PersonalDays { get; init; } = 180;

    public double For(MemoryScope scope) => scope switch
    {
        MemoryScope.Session => SessionDays,
        MemoryScope.Project => ProjectDays,
        MemoryScope.Global => GlobalDays,
        MemoryScope.Personal => PersonalDays,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown memory scope."),
    };
}
