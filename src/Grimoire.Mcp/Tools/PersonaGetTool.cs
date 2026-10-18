using System.ComponentModel;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class PersonaGetTool(PersonaService persona, MemoryActorProvider actors)
{
    public const string Name = "memory_persona_get";

    public const string Title = "Get the persona";

    public const string Description =
        "Read the persona document the human curates for their agents: identity, tone, preferences, rules. The " +
        "persona is per-tome - each tome carries its own. Call it at session start before writing in the human's " +
        "voice or making style calls, and whenever a standing instruction might exist - check, don't guess. Empty " +
        "sections were never filled by the human: do not invent content for them, and do not write persona facts " +
        "through memory_remember; the human edits persona in the Grimoire UI or CLI.";

    public Task<CallToolResult> Get(
        [Description("Tome whose persona to read; omit for your resolved tome.")]
        string? tome = null,
        CancellationToken cancellationToken = default)
    {
        if (TomeParams.Validate(tome, nameof(tome)) is { } tomeError)
        {
            return Task.FromResult(ToolResults.Failure(tomeError));
        }

        return ToolResults.ExecuteAsync(async ct =>
        {
            var resolvedTome = await actors.ResolveTomeAsync(tome, ct);
            var document = await persona.GetAsync(ct, resolvedTome);

            return ToolResults.Success(new PersonaView
            {
                Tome = resolvedTome,
                Identity = document.Identity,
                Tone = document.Tone,
                Preferences = document.Preferences,
                Rules = document.Rules,
            });
        }, cancellationToken);
    }
}
