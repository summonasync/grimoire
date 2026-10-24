namespace Grimoire.Api.Contracts;

public sealed record RenameTomeRequest
{
    public required string To { get; init; }
}
