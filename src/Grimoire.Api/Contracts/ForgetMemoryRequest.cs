namespace Grimoire.Api.Contracts;

public sealed record ForgetMemoryRequest
{
    public required string Reason { get; init; }
}
