namespace Grimoire.Core;

public static class MemoryStatuses
{
    public static IReadOnlyList<MemoryStatus> All { get; } = [.. Enum.GetValues<MemoryStatus>()];
}
