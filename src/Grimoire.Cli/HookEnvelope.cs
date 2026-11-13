using System.Text.Json.Serialization;

namespace Grimoire.Cli;

internal sealed record HookEnvelope
{
    [JsonPropertyName("hookSpecificOutput")]
    public required HookSpecificOutput HookSpecificOutput { get; init; }
}
