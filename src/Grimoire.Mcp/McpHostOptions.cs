using Grimoire.Core;

namespace Grimoire.Mcp;

public sealed record McpHostOptions
{
    public const string TomeEnvironmentVariable = "GRIMOIRE_TOME";

    public const string DefaultInstructions =
        "Grimoire is the local memory service of this machine. At session start call memory_brief: it loads your " +
        "tome's persona, standing facts, recent journal and tombstones in one read; use memory_recall for targeted " +
        "queries, memory_list to browse by filter, and memory_persona_get for the full persona document. While " +
        "working call memory_remember the moment a decision, preference, correction or non-obvious constraint " +
        "becomes known, not when the session ends. When a memory turns out wrong, memory_forget tombstones it; " +
        "memory_restore reverses a correction that was itself a mistake, and memory_history shows a fact's " +
        "replacement lineage before you trust or overwrite it. When your tome lacks the answer, memory_scry reads " +
        "other tomes anonymously (facts by default, journal opt-in). When memory_stats reports pendingCompaction " +
        "above zero, run the memory_compact_candidates distillation loop. Before asserting anything that might " +
        "already be on record, check memory_recall first.";

    public string ServerName { get; init; } = "grimoire";

    public string ServerTitle { get; init; } = "Grimoire Memory";

    public string ServerVersion { get; init; } = "0.1.0";

    public IReadOnlyList<ProjectAliasRecord> Projects { get; init; } = [];

    public MemoryActor? Actor { get; init; }

    public string? EnvTome { get; init; }

    public string? EnvActor { get; init; }

    public string Instructions { get; init; } = DefaultInstructions;
}
