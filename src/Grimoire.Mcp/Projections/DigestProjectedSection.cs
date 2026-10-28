using Grimoire.Core;

namespace Grimoire.Mcp.Projections;

internal sealed record DigestProjectedSection
{
    public required BriefSectionState State { get; init; }

    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Items { get; init; }
}
