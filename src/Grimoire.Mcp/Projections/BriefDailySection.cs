using Grimoire.Core;

namespace Grimoire.Mcp.Projections;

internal sealed record BriefDailySection
{
    public required BriefSectionState State { get; init; }

    public required IReadOnlyList<BriefDailyGroupItem> Groups { get; init; }

    public int More { get; init; }
}
