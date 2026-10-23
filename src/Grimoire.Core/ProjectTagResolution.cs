namespace Grimoire.Core;

public sealed record ProjectTagResolution
{
    public string? ProjectId { get; init; }

    public bool AutoTagged { get; init; }

    public ProjectTagRejection? Rejection { get; init; }

    public IReadOnlyList<string> AllowedAliases { get; init; } = [];
}
