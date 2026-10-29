using System.Text.Json.Serialization;

namespace Grimoire.Api.Contracts;

public sealed record ProjectedMemoryListResponse(
    string Tome,
    int Count,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Items)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Warnings { get; init; }
}
