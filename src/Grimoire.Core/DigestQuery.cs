namespace Grimoire.Core;

public sealed record DigestQuery
{
    public required string Topic { get; init; }

    public string? Tome { get; init; }

    public DateTimeOffset? CreatedAfter { get; init; }

    public DateTimeOffset? CreatedBefore { get; init; }

    public int? Limit { get; init; }
}
