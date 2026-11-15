using System.Text.Json.Serialization;

namespace Grimoire.Core;

public sealed record RecallResult
{
    public required IReadOnlyList<RecallMatch> Matches { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Warnings { get; init; }
}
