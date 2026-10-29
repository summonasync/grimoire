using System.Text.Json.Serialization;
using Grimoire.Core;

namespace Grimoire.Api.Contracts;

public sealed record MemoryListResponse(string Tome, int Count, IReadOnlyList<MemoryRecord> Items)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Warnings { get; init; }
}
