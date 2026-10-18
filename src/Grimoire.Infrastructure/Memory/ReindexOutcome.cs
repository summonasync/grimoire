namespace Grimoire.Infrastructure.Memory;

public readonly record struct ReindexOutcome(int Processed, int Reembedded, int Skipped);
