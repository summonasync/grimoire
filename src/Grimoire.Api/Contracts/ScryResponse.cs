using System.Text.Json.Serialization;

namespace Grimoire.Api.Contracts;

public sealed record ScryResponse(
    IReadOnlyList<string> Tomes,
    ScryAttribution Attribution,
    int Count,
    IReadOnlyList<ScryItem> Items,
    long ElapsedMs)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Warnings { get; init; }
}
