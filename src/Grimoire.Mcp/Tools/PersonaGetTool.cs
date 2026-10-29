using System.ComponentModel;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class PersonaGetTool(PersonaService persona, IMemoryStore store, MemoryActorProvider actors)
{
    public const string Name = "memory_persona_get";

    public const string Title = "Get the persona";

    public const string Description =
        "Read the human-curated persona of one tome: identity, tone, preferences, rules. Check it before writing " +
        "in the human's voice or making style calls; empty sections were never filled - do not invent them, and " +
        "persona is edited by the human in the UI or CLI, never through these memory tools.";

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
                Warnings = await EmptyTomeWarning.BuildAsync(
                    store, IsEmpty(document), tome, resolvedTome, ct),
            });
        }, cancellationToken);
    }

    private static bool IsEmpty(PersonaDocument document) =>
        document.Identity.Length == 0 &&
        document.Tone.Length == 0 &&
        document.Preferences.Length == 0 &&
        document.Rules.Length == 0;
}
