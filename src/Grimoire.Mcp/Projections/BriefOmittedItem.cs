using Grimoire.Core;

namespace Grimoire.Mcp.Projections;

internal sealed record BriefOmittedItem
{
    public required Guid MemoryId { get; init; }

    public required string Excerpt { get; init; }

    public required BriefOmissionReason Reason { get; init; }

    public bool ExcerptCut { get; init; }
}
