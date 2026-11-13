namespace Grimoire.Core;

public sealed record BriefOmittedRecord
{
    public required Guid MemoryId { get; init; }

    public required string Excerpt { get; init; }

    public required BriefOmissionReason Reason { get; init; }

    public bool ExcerptCut { get; init; }
}
