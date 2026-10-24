namespace Grimoire.Core;

public sealed record RecordGraphOptions
{
    public bool IncludeCoMembership { get; init; }

    public double? RelatedSimilarityThreshold { get; init; }
}
