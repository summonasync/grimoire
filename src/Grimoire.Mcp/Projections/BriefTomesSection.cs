using Grimoire.Core;

namespace Grimoire.Mcp.Projections;

internal sealed record BriefTomesSection
{
    public required BriefSectionState State { get; init; }

    public required IReadOnlyList<BriefTomeItem> Items { get; init; }

    public int More { get; init; }

    public string? Note { get; init; }
}
