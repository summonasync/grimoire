namespace Grimoire.Mcp.Projections;

internal sealed record PersonaView
{
    public string Identity { get; init; } = string.Empty;

    public string Tone { get; init; } = string.Empty;

    public string Preferences { get; init; } = string.Empty;

    public string Rules { get; init; } = string.Empty;
}
