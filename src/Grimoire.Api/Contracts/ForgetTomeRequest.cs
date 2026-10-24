namespace Grimoire.Api.Contracts;

public sealed record ForgetTomeRequest
{
    public required string Reason { get; init; }
}
