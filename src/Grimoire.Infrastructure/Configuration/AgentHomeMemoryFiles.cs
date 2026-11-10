namespace Grimoire.Infrastructure.Configuration;

public static class AgentHomeMemoryFiles
{
    public static IReadOnlyList<string> Locate(string homeRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeRoot);

        return
        [
            Path.Combine(homeRoot, ".claude", "CLAUDE.md"),
            Path.Combine(homeRoot, ".qwen", "QWEN.md"),
            Path.Combine(homeRoot, ".codex", "AGENTS.md"),
            Path.Combine(homeRoot, ".kimi-code", "AGENTS.md"),
        ];
    }
}
