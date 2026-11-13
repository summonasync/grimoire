using Grimoire.Core;

namespace Grimoire.Api.Querying;

public sealed record MemoryQueryResult
{
    public required IReadOnlyList<MemoryRecord> Records { get; init; }

    public IReadOnlyList<string>? Warnings { get; init; }
}
