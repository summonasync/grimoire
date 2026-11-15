using System.Text.Json.Serialization;

namespace Grimoire.Cli;

internal sealed record HookSpecificOutput
{
    [JsonPropertyName("hookEventName")]
    public required string HookEventName { get; init; }

    [JsonPropertyName("additionalContext")]
    public required string AdditionalContext { get; init; }
}
