namespace Grimoire.Api.Contracts;

public sealed record RetomeRecordsRequest
{
    public required IReadOnlyList<Guid> Ids { get; init; }

    public required string To { get; init; }
}
