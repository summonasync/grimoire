namespace Grimoire.Api.Contracts;

public sealed record PatchMemoryRequest
{
    public string? Content { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

    public double? Importance { get; init; }

    public string? Reason { get; init; }

    public bool IsEmpty => Content is null && Tags is null && Importance is null;
}
