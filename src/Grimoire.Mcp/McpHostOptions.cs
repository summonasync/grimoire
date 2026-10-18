using Grimoire.Core;

namespace Grimoire.Mcp;

public sealed record McpHostOptions
{
    public const string DefaultInstructions =
        "Grimoire is the local memory service of this machine. At session start call memory_persona_get, " +
        "then memory_recall for project and global scope to load standing decisions and preferences before working. " +
        "While working call memory_remember the moment a decision, preference, correction or non-obvious constraint " +
        "becomes known, not when the session ends. When memory_stats reports pendingCompaction above zero, run the " +
        "memory_compact_candidates distillation loop. Before asserting anything that might already be on record, " +
        "check memory_recall first.";

    public string ServerName { get; init; } = "grimoire";

    public string ServerTitle { get; init; } = "Grimoire Memory";

    public string ServerVersion { get; init; } = "0.1.0";

    public MemoryActor? Actor { get; init; }

    public string Instructions { get; init; } = DefaultInstructions;
}
