using Grimoire.Core;

namespace Grimoire.Mcp.Projections;

internal sealed record BriefPersonaSection
{
    public required BriefSectionState State { get; init; }

    public PersonaView? Document { get; init; }
}
