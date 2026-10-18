using System.Text.Json.Serialization;
using Grimoire.Core;

namespace Grimoire.Api.Contracts;

public sealed record ScryItem
{
    public required Guid MemoryId { get; init; }

    public required string Content { get; init; }

    public required string Tome { get; init; }

    public required MemoryScope Scope { get; init; }

    public required MemoryKind Kind { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Actor { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string? ProjectId { get; init; }

    public required double Similarity { get; init; }

    public required double Score { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
