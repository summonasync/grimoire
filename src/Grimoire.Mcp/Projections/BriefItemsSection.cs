using Grimoire.Core;

namespace Grimoire.Mcp.Projections;

internal sealed record BriefItemsSection<T>
{
    public required BriefSectionState State { get; init; }

    public required IReadOnlyList<T> Items { get; init; }
}
