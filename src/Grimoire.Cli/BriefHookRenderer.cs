using System.Text.Json;
using Grimoire.Core;

namespace Grimoire.Cli;

internal static class BriefHookRenderer
{
    public static void Write(CliEnvironment environment, BriefResult brief, bool daily)
    {
        var buffer = new StringWriter();
        MemoryFormatter.WriteBriefSections(buffer, brief, daily);
        if (daily)
        {
            MemoryFormatter.WriteBriefWarnings(environment.Error, brief);
        }

        var envelope = new HookEnvelope
        {
            HookSpecificOutput = new()
            {
                HookEventName = "SessionStart",
                AdditionalContext = TagFree(buffer.ToString().TrimEnd()),
            },
        };
        environment.Output.WriteLine(JsonSerializer.Serialize(envelope));
    }

    // Qwen Code 0.27.1 HTML-escapes '<' and '>' inside additionalContext (measured, not inferred), so the
    // payload must arrive tag-free. Board XML markers, removed-tombstones and alias+vendor placeholders
    // reword to their square-bracket twins: same width, same reading, and no second escaping pass to mangle.
    private static string TagFree(string text) =>
        text.Replace('<', '[').Replace('>', ']');
}
