using Grimoire.Core;

namespace Grimoire.Api.Contracts;

public sealed record UpdateSettingsRequest
{
    public static readonly IReadOnlyList<string> EditableKeys =
    [
        "memory.sessionTtlHours",
        "memory.dedupSimilarityThreshold",
        "memory.tombstoneRetentionDays",
        "memory.recallOversampling",
        "memory.recencyHalfLives",
        "memory.compaction",
    ];

    public double? SessionTtlHours { get; init; }

    public double? DedupSimilarityThreshold { get; init; }

    public double? TombstoneRetentionDays { get; init; }

    public int? RecallOversampling { get; init; }

    public RecencyHalfLives? RecencyHalfLives { get; init; }

    public CompactionOptions? Compaction { get; init; }
}
