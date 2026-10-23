namespace Grimoire.Infrastructure.Memory;

public readonly record struct ReindexOutcome(
    int Processed,
    int Reembedded,
    int Skipped,
    int Discarded = 0,
    bool Recreated = false);
