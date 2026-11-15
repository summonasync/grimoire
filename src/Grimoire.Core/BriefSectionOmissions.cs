namespace Grimoire.Core;

public sealed record BriefSectionOmissions
{
    public required int Count { get; init; }

    public required IReadOnlyList<BriefOmittedRecord> Dropped { get; init; }
}
