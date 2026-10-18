namespace Grimoire.Core;

public static class MemoryScopes
{
    public static IReadOnlyList<MemoryScope> LongTerm { get; } = [MemoryScope.Project, MemoryScope.Global, MemoryScope.Personal];

    public static IReadOnlyList<MemoryScope> All { get; } = [MemoryScope.Session, .. LongTerm];
}
