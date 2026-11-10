using System.ComponentModel;
using Grimoire.Core;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class TomeCreateTool(TomeManifestService manifests, MemoryActorProvider actors)
{
    public const string Name = "memory_tome_create";

    public const string Title = "Create a tome manifest";

    public const string Description =
        "Create a tome manifest so memory_brief's tome table can route agents. Check the tome table first and " +
        "write to the fitting tome; create only when no tome fits. key is a lowercase kebab-case tome key; " +
        "description (1..280 chars after trim) must say what belongs there. Hard delete and manifest edit are " +
        "human-only - no agent verbs exist for them.";

    public Task<CallToolResult> Create(
        [Description("Lowercase kebab-case tome key, 1..64 chars; normalized before create.")]
        string key,
        [Description("What belongs in this tome, 1..280 chars after trim.")]
        string description,
        CancellationToken cancellationToken = default)
    {
        if (!TomeKey.TryNormalize(key, out var normalized))
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidTome,
                $"key must be a lowercase kebab-case tome key; '{key}' is not a valid tome key.",
                nameof(key)).NothingApplied()));
        }

        var trimmed = description?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidDescription,
                "description must be 1..280 characters after trim; the submitted description is empty.",
                nameof(description)).NothingApplied()));
        }

        if (trimmed.Length > TomeManifestService.MaxDescriptionLength)
        {
            return Task.FromResult(ToolResults.Failure(new ToolError(
                ToolErrorCodes.InvalidDescription,
                $"description must be 1..280 characters after trim; the submitted description is {trimmed.Length} characters long.",
                nameof(description)).NothingApplied()));
        }

        return ToolResults.ExecuteAsync(async ct =>
        {
            var result = await manifests.CreateAsync(normalized, trimmed, await actors.GetActorAsync(ct), ct);
            if (result.Outcome != TomeManifestOutcome.Created)
            {
                return ToolResults.Failure(new ToolError(
                    ToolErrorCodes.TomeManifestExists,
                    $"Tome '{normalized}' already has a manifest; write into it instead of recreating it.",
                    nameof(key)).NothingApplied());
            }

            var manifest = result.Manifest!;
            return ToolResults.Success(new TomeCreateView
            {
                Tome = manifest.Key,
                Description = manifest.Description,
                CreatedBy = manifest.CreatedBy,
                CreatedAt = manifest.CreatedAt,
                UpdatedAt = manifest.UpdatedAt,
            });
        }, cancellationToken);
    }
}
