using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class PersonaGetTool(PersonaService persona)
{
    public const string Name = "memory_persona_get";

    public const string Title = "Get the persona";

    public const string Description =
        "Read the persona document the human curates for their agents: identity, tone, preferences, rules. Call it " +
        "at session start before writing in the human's voice or making style calls, and whenever a standing " +
        "instruction might exist - check, don't guess. Empty sections were never filled by the human: do not invent " +
        "content for them, and do not write persona facts through memory_remember; the human edits persona in the " +
        "Grimoire UI or CLI.";

    public Task<CallToolResult> Get(CancellationToken cancellationToken = default)
    {
        return ToolResults.ExecuteAsync(async ct =>
        {
            var document = await persona.GetAsync(ct);

            return ToolResults.Success(new PersonaView
            {
                Identity = document.Identity,
                Tone = document.Tone,
                Preferences = document.Preferences,
                Rules = document.Rules,
            });
        }, cancellationToken);
    }
}
