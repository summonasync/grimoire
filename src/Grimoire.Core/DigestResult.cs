namespace Grimoire.Core;

public sealed record DigestResult
{
    public required string Topic { get; init; }

    public required BriefSectionState LongMemoState { get; init; }

    public required IReadOnlyList<DigestHit> LongMemo { get; init; }

    public required BriefSectionState RecentState { get; init; }

    public required IReadOnlyList<DigestHit> Recent { get; init; }

    public required BriefSectionState GoneState { get; init; }

    public required IReadOnlyList<DigestHit> Gone { get; init; }

    public required IReadOnlyList<DigestSection> Omitted { get; init; }
}
