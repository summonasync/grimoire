namespace Grimoire.Api.Contracts;

public sealed record MergeTomeRequest
{
    public required string Into { get; init; }
}
